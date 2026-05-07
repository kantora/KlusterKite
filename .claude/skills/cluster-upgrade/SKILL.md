---
name: cluster-upgrade
description: Use when upgrading a running KlusterKite local devcluster after code/HOCON/Docker-image changes. Idiomatic flow is `cake FinalPushLocalPackages` (auto-bumps the patch and pushes a new package set) followed by a Configuration migration driven through the GraphQL API. Skill ships with a Python orchestrator that does the migration end-to-end.
---

KlusterKite was designed to upgrade in place via configuration migrations, not by replacing running containers. Each cluster Configuration pins a set of `PackageRequirements` and the resolved `PackagesToInstall` per template. To roll new code:

1. **Bump versions** so old and new packages can coexist on the feed.
2. **Build and push** to the local NuGet feed.
3. **Create a new Configuration** that picks up the new versions and migrate the cluster to it.

Containers stay running throughout. Migration handles draining, re-launching, and rolls back on failure.

The `--force-recreate` and DB-wipe paths in [§ Pitfalls](#5-pitfalls) exist for two narrow cases: cold start before the cluster is seeded, and developer-side iteration on a single package version (`0.0.0-local`) where you don't want to bump on every save.

## 1. Idiomatic upgrade

### a. Bump version + push

`build.cake`'s `SetVersion` target queries the local NuGet feed for the highest existing patch and increments by one (`0.0.5-local` → `0.0.6-local`). The composite target that bumps, builds, packs, and pushes:

```bash
NUGET_API_KEY=KlusterKite NUGET_SERVER_URL=http://localhost:28081 \
  dotnet cake build.cake --target=FinalPushLocalPackages
```

Adjust the port to whatever `Docker/KlusterKite/.env` says (`28081` in this checkout's parameterized layout, `81` upstream). After the run, the local NuGet feed has the new versioned packages. The currently-running cluster is unaffected — it's still pinned to the old Configuration's `PackagesToInstall`.

If the change includes the **launcher binary itself** (anything under `KlusterKite.NodeManager.Launcher.*` or `KlusterKite.NodeManager.Seeder.Launcher`), you also need new Docker images:

```bash
dotnet cake build.cake --target=FinalBuildDocker
```

### b. Drive the migration via API

Use `upgrade.py` (next to this file). It clones the current Active configuration into a new Draft with the version bumped and the `packages` list refreshed from the live nuget feed, then runs Check → SetReady → Start migration → walks the migration steps → Finish.

```bash
# Plan only; prints the new settings preview and exits without mutating
python .claude/skills/cluster-upgrade/upgrade.py --dry-run

# Real upgrade. Defaults: --bump minor, --name "Release MAJ.MIN"
python .claude/skills/cluster-upgrade/upgrade.py

# Fully parameterized
python .claude/skills/cluster-upgrade/upgrade.py \
    --api http://localhost:28080 --bump minor \
    --name "Release 0.4" --notes "akka 1.5.68" \
    --abort-current        # cancel any in-flight migration first
```

Auth defaults to `admin/admin` against `KlusterKite.NodeManager.WebApplication` (override with `--user`/`--password`/`--client-id` or env vars `KK_USER`/`KK_PASSWORD`/`KK_CLIENT_ID`).

### c. What the script does, in GraphQL terms

If you need to drive part of the flow by hand, the mutation surface is:

| Step | Mutation |
|---|---|
| Get auth | `POST /api/1.x/security/token` (form: `grant_type=password&...`) |
| Read Active | `query { api { klusterKiteNodesApi { configurations(filter:{state:Active},limit:1) { ... } } } }` |
| Read latest pkgs | `query { api { klusterKiteNodesApi { nugetPackages { edges { node { name version } } } } } }` |
| Create draft | `klusterKiteNodeApi_klusterKiteNodesApi_configurations_create(input:{newNode:{majorVersion,minorVersion,name,notes}})` |
| Write settings | `klusterKiteNodeApi_klusterKiteNodesApi_configurations_update(input:{id, newNode:{id,settings:{...}}})` |
| Validate | `klusterKiteNodeApi_klusterKiteNodesApi_configurations_check(input:{id})` |
| Draft → Ready | `klusterKiteNodeApi_klusterKiteNodesApi_configurations_setReady(input:{id})` |
| Start migration | `klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_migrationCreate(input:{newConfigurationId})` |
| Migrate resources | `..._clusterManagement_migrationResourceUpdate(input:{request:{resources:[{templateCode,migratorTypeName,resourceCode,target}]}})` (`target ∈ {Source,Destination}`) |
| Migrate nodes | `..._clusterManagement_migrationNodesUpdate(input:{target:Destination})` |
| Finish | `..._clusterManagement_migrationFinish(input:{})` |
| Cancel | `..._clusterManagement_migrationCancel(input:{})` |

Input shapes for `Configuration_Input` / `ConfigurationSettings_Input` / `NodeTemplate_Input` / `MigratorTemplate_Input` / `PackageRequirement_Input` / `PackageDescription_Input` are introspectable on the running cluster:

```bash
curl -s -X POST -H 'Content-Type: application/json' -H "Authorization: Bearer $TOKEN" \
  -d '{"query":"{__type(name:\"KlusterKiteNodeApi_ConfigurationSettings_Input\"){inputFields{name type{kind name ofType{kind name}}}}}"}' \
  http://localhost:28080/api/1.x/graphQL | python -m json.tool
```

Two GraphQL ↔ JSON quirks worth knowing:
- The output schema renames `id` → `_id` on all sub-API types (Relay collision; PR #19 fix). The **input** types still use plain `id`. So when you query for a configuration's packages you get `{_id, version}`, but you must send `{id, version}` back via `configurations_update`.
- `configurations_check` and the migration mutations return their domain errors inside `errors{ edges{ node{ field message } } }` rather than as transport-level GraphQL errors. The script treats those payload errors as success unless they're non-empty.

### d. Verify

```bash
# Auth round-trip
curl -s --max-time 8 -X POST -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password&username=admin&password=admin&client_id=KlusterKite.NodeManager.WebApplication' \
  http://localhost:28080/api/1.x/security/token

# Active configuration is the new one
TOKEN=$(curl -s -X POST -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password&username=admin&password=admin&client_id=KlusterKite.NodeManager.WebApplication' \
  http://localhost:28080/api/1.x/security/token | python -c "import json,sys; print(json.load(sys.stdin)['access_token'])")
curl -s -X POST -H 'Content-Type: application/json' -H "Authorization: Bearer $TOKEN" \
  -d '{"query":"{api{klusterKiteNodesApi{configurations(filter:{state:Active},limit:1){edges{node{_id,name,majorVersion,minorVersion}}}}}}"}' \
  http://localhost:28080/api/1.x/graphQL
```

## 2. Cold start (no Active configuration yet)

Different problem from upgrade. On first boot of a fresh devcluster:

1. `nuget` (image) starts empty.
2. `seeder` waits for `RequiredPackages` to appear on NuGet (retry loop in seeder.launcher).
3. `dotnet cake build.cake --target=FinalPushLocalPackages` populates the feed.
4. Seeder resolves `PackageRequirements` → writes Initial Configuration to the DB and fallback JSON files to `/fallback/`.
5. `manager`, `worker`, `publisher*` start, fetch their NodeStartUpConfiguration from the API (or the fallback JSON if the API is unreachable), download packages, exec `KlusterKite.Core.Service`.

After PR #20 the seeder is a hard blocker: any package resolution error throws and the launcher retries until the feed catches up. After PR #20 the seeder also re-resolves Active and Ready configurations against the current NuGet feed on every run, so a cold restart against newer packages converges automatically.

## 3. Debug-only shortcut: skip versioning, force-replace `0.0.0-local`

When iterating on one package without wanting to bump versions, you can overwrite `0.0.0-local` directly. **This is not an upgrade** — it bypasses the migration mechanism, doesn't run `ConfigurationCheckActor`, and leaves the DB-stored `PackagesToInstall` referring to whatever versions the live nodes happen to have downloaded.

Only use it when:
- The package's published version is still `0.0.0-local` (default outside CI),
- You're sure no other `PackagesToInstall` row in any Configuration references the changed package's transitive surface,
- You'll verify behavior immediately and not leave the cluster in this state.

```bash
# 1. Build (no SetVersion, so version stays at 0.0.0-local)
NUGET_API_KEY=KlusterKite NUGET_SERVER_URL=http://localhost:28081 \
  dotnet cake build.cake --target=Nuget

# 2. simple-nuget-server returns 409 on duplicate version. Wipe row + file.
PKG_ID=KlusterKite.API.Provider                  # ← only the changed package(s)
docker exec klusterkite-nuget-1 python -c "
import sqlite3
c=sqlite3.connect('/var/www/db/packages.sqlite3')
c.execute('delete from versions where PackageId = ?', ('$PKG_ID',))
c.execute('delete from packages where PackageId = ?', ('$PKG_ID',))
c.commit()"
docker exec klusterkite-nuget-1 sh -c "rm -rf /var/www/packagefiles/$PKG_ID"
dotnet nuget push temp/packageOut/$PKG_ID.0.0.0-local.nupkg \
  --source http://localhost:28081 --api-key KlusterKite \
  --allow-insecure-connections

# 3. Recreate consumers so they re-download.
#    manager + worker run providers; publishers run GraphQL.Publisher; seeder runs the seed dll.
cd Docker/KlusterKite
docker compose -p klusterkite up -d --force-recreate <services...>
```

The recreate cascades to `seeder` (`service_completed_successfully` dep). After PR #20 the seeder will refresh Active and Ready configs against the new NuGet state, which keeps things consistent — but that's a safety net, not the upgrade path.

## 4. Decision quick-reference

| Change | Use this path |
|---|---|
| Code in any package | §1 — bump version, push, migrate via `upgrade.py` |
| One package, debugging an iteration | §3 — overwrite `0.0.0-local` and recreate consumers |
| Fresh checkout, no Active config | §2 — cold start (push packages, let seeder run) |
| Launcher binary (`KlusterKite.NodeManager.Launcher.*`) | `cake FinalBuildDocker`, then §1 |
| HOCON inside `KlusterKite.NodeManager/Resources/akka.hocon` | Same as code change to that package |
| HOCON copied into a Docker image (`Docker/KlusterKite*/seeder.hocon`) | `docker buildx build` of that dir, recreate that container |
| `docker-compose.yml` | `docker compose up -d --force-recreate <svc>`, no build |
| React app | `npm run build` in `klusterkite-web` → image build → recreate `monitoringUI` |

## 5. Pitfalls

### Migration stuck in `Preparing`
The chosen target Configuration's `PackagesToInstall` doesn't have the runtime's framework key (`.NETCoreApp,Version=v9.0`). Causes: it was Check'd against an old `SupportedFrameworks` list (pre-PR #20), or `MigrationActor` can't extract the migrator template service.

`upgrade.py` handles this: it always runs `configurations_check` against the live `SupportedFrameworks` before SetReady, so the framework keys match what `MigrationActor` will look up. If you took the path manually and the configuration was Check'd before PR #20, cancel and re-Check:

```bash
# Cancel (UI hides the button while migrationSteps is null; API works regardless)
python .claude/skills/cluster-upgrade/upgrade.py --abort-current --dry-run
# (or call migrationCancel directly via curl, see §1c)

# After cancel, recreate seeder so PR #20's refresh repopulates PackagesToInstall
docker compose -p klusterkite up -d --force-recreate seeder
```

### `seed` container exits, `manager` grabs `172.18.0.6`
The cluster seed image's process can exit cleanly post-bootstrap. Its reserved IP is freed, and a recreated `manager` (no fixed IP) can take it. Subsequent `up -d seed` then fails with "Address already in use".

```bash
docker stop klusterkite-manager-1
docker compose -p klusterkite up -d seed
docker compose -p klusterkite up -d manager
```

### nuget container nginx doesn't bind on cold start
`klusterkite-nuget-1` runs hhvm + nginx via supervisord. Nginx fails to bind `:80` on first start; `ss -tln` inside shows only `:9000`. A reload fixes it:

```bash
docker exec klusterkite-nuget-1 nginx -s reload
```

Symptom: seeder logs `Connection refused (nuget:80)` in retry loop.

### Package wipe partially succeeded
If you wiped the `versions` row but not `/var/www/packagefiles/<pkg>/<ver>.nupkg` (or vice versa), the next push 409s and the search index gets out of sync. Always do BOTH steps in §3.

### MonitoringUI shows stale UI after rebuild
Browser caches the bundled JS. Hard-reload (Ctrl/Cmd-Shift-R). The bundled JS filename hash changes per build; cached URLs return 404 after a refresh — that's the signal the new bundle is live.

### Two clusters fighting for ports / IPs
The `forge-*` cluster and `klusterkite-*` share `klusterkite/*:latest` images but use different Docker networks (172.18.x vs 172.19.x) and different host ports via `Docker/KlusterKite/.env` (80→28080 / 81→28081 / 82→28082 / 9200→28200 / 5601→28601 / 1194→28194). `docker compose down` in one project doesn't kill the other. Rebuilding `klusterkite/*:latest` doesn't affect already-running containers — they hold their pinned image ID until recreate.

### `dotnet nuget push` rejects HTTP
PR #17 added `--allow-insecure-connections` to every Cake push site. If you're pushing manually outside of Cake, include the flag.
