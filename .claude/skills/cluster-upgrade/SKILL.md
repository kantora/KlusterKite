---
name: cluster-upgrade
description: Use when refreshing a running KlusterKite local devcluster after code/HOCON/Docker-image changes. Idiomatic flow is one Cake target — `LocalDevClusterUpdate` — that bumps the patch, pushes new packages, clones the Active configuration, and migrates the cluster end-to-end. Skill points at the target and documents the narrow cases where you'd take a different path.
---

KlusterKite was designed to upgrade in place via configuration migrations, not by replacing running containers. Each cluster Configuration pins a set of `PackageRequirements` and the resolved `PackagesToInstall` per template. To roll new code:

1. **Bump versions** so old and new packages can coexist on the feed.
2. **Build and push** to the local NuGet feed.
3. **Create a new Configuration** that picks up the new versions and migrate the cluster to it.

Containers stay running throughout. Migration handles draining, re-launching, and rolls back on failure.

## 1. Idiomatic upgrade — `LocalDevClusterUpdate`

Run the one composite target. It is fully deterministic; no operator approvals between steps.

```bash
NUGET_API_KEY=KlusterKite NUGET_SERVER_URL=http://localhost:28081 \
  dotnet cake build.cake --target=LocalDevClusterUpdate --notes="..."
```

What it does:

1. `FinalPushLocalPackages` — auto-bumps the patch (`0.0.5-local` → `0.0.6-local`), builds Release, packs, pushes everything to the local NuGet feed.
2. Authenticates against `/api/1.x/security/token` (defaults to `admin/admin` against `KlusterKite.NodeManager.WebApplication`).
3. Reads the Active Configuration, queries `nugetPackages` for the latest versions on the feed, and clones the Active settings into a new Draft with the version bumped (`--bump=minor` by default; pass `major` or `none`) and the `packages` list refreshed.
4. `configurations_check` → `setReady` → `migrationCreate`.
5. Walks the migration: per-template forward `migrationResourceUpdate` calls, `migrationNodesUpdate(Destination)`, manual `upgradeNode` for any min-instance pinned templates the cluster won't auto-cycle (the same restart the home page's per-node Reset button calls), then `migrationFinish`.
6. Verifies `currentMigration` cleared, the new Configuration is Active, the previous one is Archived, and zero `isObsolete: true` nodes remain.

If any step fails, the target throws — the Cake exit code tells you the cluster's state didn't reach the target. The cluster is left where it stopped (typically with a Draft you can inspect or delete and a `currentMigration` either rolled back or cancellable via the API).

### Arguments

| Flag | Default | Notes |
|---|---|---|
| `--notes` | `""` | The Configuration's audit trail. Empty is allowed for human iteration; **agents should always pass meaningful notes** describing what's actually changing (package version delta, bug fixed, issue/PR number). |
| `--name` | `Release YYYYMMDDHHMMSS` (UTC) | Human label shown in the Configurations list. |
| `--bump` | `minor` | `major` resets minor to 0; `none` keeps versions (rarely useful). The package patch is independent — that always bumps via `SetVersion`. |
| `--api` | `http://localhost:28080` (env: `KK_API_URL`) | Cluster's external HTTP endpoint. |
| `--user` / `--password` / `--client-id` | `admin` / `admin` / `KlusterKite.NodeManager.WebApplication` (env: `KK_USER`, `KK_PASSWORD`, `KK_CLIENT_ID`) | Auth. |
| `--timeout` | `900` | Seconds for any single phase (resource update, node update, migration finish). |

### Notes on `--notes`

The Configuration's notes field is the only human-readable audit trail of what changed in each migration — surfaced in the Configurations list and on the Configuration page. Useful examples:

- `"Akka 1.5.67 → 1.5.68; refresh after PackageUtils.Search null fix"`
- `"#issue-42: ConfigurationCheckActor INVALID_OPERATION on null enum"`
- `"GraphQL.Publisher 0.1.3-local; rolls back PR #26 hotfix that broke MigrationSteps"`

Useless examples (don't ship as an agent): `"update"`, `"bump"`, `""`, `"."`.

### When to also rebuild Docker images

If your change includes the **launcher binary** (anything under `KlusterKite.NodeManager.Launcher.*` or `KlusterKite.NodeManager.Seeder.Launcher`) or any HOCON copied into a Docker image (`Docker/KlusterKite*/seeder.hocon`, `Docker/KlusterKite*/start-publisher.hocon`, etc.), `LocalDevClusterUpdate` is not enough — those binaries live in the image, not in NuGet packages. Run the image build first, then the target:

```bash
dotnet cake build.cake --target=FinalBuildDocker
NUGET_API_KEY=KlusterKite NUGET_SERVER_URL=http://localhost:28081 \
  dotnet cake build.cake --target=LocalDevClusterUpdate --notes="..."
```

## 2. Cold start (no Active configuration yet)

Different problem from upgrade. On first boot of a fresh devcluster:

1. `nuget` (image) starts empty.
2. `seeder` waits for `RequiredPackages` to appear on NuGet (retry loop in seeder.launcher).
3. `dotnet cake build.cake --target=FinalPushLocalPackages` populates the feed.
4. Seeder resolves `PackageRequirements` → writes Initial Configuration to the DB and fallback JSON files to `/fallback/`.
5. `manager`, `worker`, `publisher*` start, fetch their NodeStartUpConfiguration from the API (or the fallback JSON if the API is unreachable), download packages, exec `KlusterKite.Core.Service`.

After PR #20 the seeder is a hard blocker: any package resolution error throws and the launcher retries until the feed catches up. The seeder also re-resolves Active and Ready configurations against the current NuGet feed on every run, so a cold restart against newer packages converges automatically.

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

| Change | Path |
|---|---|
| Code in any package | §1 — `dotnet cake build.cake --target=LocalDevClusterUpdate --notes="..."` |
| One package, debugging an iteration | §3 — overwrite `0.0.0-local` and recreate consumers |
| Fresh checkout, no Active config | §2 — cold start (push packages, let seeder run) |
| Launcher binary (`KlusterKite.NodeManager.Launcher.*`) | `cake FinalBuildDocker`, then §1 |
| HOCON inside `KlusterKite.NodeManager/Resources/akka.hocon` | Same as code change to that package |
| HOCON copied into a Docker image (`Docker/KlusterKite*/seeder.hocon`) | `docker buildx build` of that dir, recreate that container |
| `docker-compose.yml` | `docker compose up -d --force-recreate <svc>`, no build |
| React app | `npm run build` in `klusterkite-web` → image build → recreate `monitoringUI` |

## 5. Driving the API by hand (rare)

Most of the time you should not need this — `LocalDevClusterUpdate` covers the full happy path. Reach for it only when:
- Diagnosing why the target failed mid-migration and you want to inspect the live state.
- Cancelling a stuck migration that the target left behind (its Cake exit doesn't auto-cancel).
- Custom flows the target doesn't support (rolling forward only some templates, switching back to a Source state, etc.).

Auth, then fire any of:

```bash
TOKEN=$(curl -s -X POST -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password&username=admin&password=admin&client_id=KlusterKite.NodeManager.WebApplication' \
  http://localhost:28080/api/1.x/security/token | python -c "import json,sys; print(json.load(sys.stdin)['access_token'])")

# Inspect state
curl -s -X POST -H 'Content-Type: application/json' -H "Authorization: Bearer $TOKEN" \
  -d '{"query":"{api{klusterKiteNodesApi{
        clusterManagement{currentMigration{state}}
        configurations(sort:[id_desc],limit:3){edges{node{_id name state}}}
        getActiveNodeDescriptions{edges{node{isObsolete nodeTemplate}}}
      }}}"}' \
  http://localhost:28080/api/1.x/graphQL | python -m json.tool

# Cancel an in-flight migration
curl -s -X POST -H 'Content-Type: application/json' -H "Authorization: Bearer $TOKEN" \
  -d '{"query":"mutation{ klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_migrationCancel(input:{clientMutationId:\"x\"}){ result } }"}' \
  http://localhost:28080/api/1.x/graphQL
```

Mutation reference (the same surface `LocalDevClusterUpdate` uses internally):

| Step | Mutation | Payload type |
|---|---|---|
| Create draft | `..._configurations_create(input:{newNode:{majorVersion,minorVersion,name,notes}})` | `Configuration_NodeMutationPayload` (selects `node{_id} errors{edges{node{field message}}}`) |
| Write settings | `..._configurations_update(input:{id, newNode:{id,settings:{...}}})` | same |
| Validate | `..._configurations_check(input:{id})` | same |
| Draft → Ready | `..._configurations_setReady(input:{id})` | same |
| Start migration | `..._clusterManagement_migrationCreate(input:{newConfigurationId})` | `MutationResult_Migration__MutationPayload` (errors live under inner `result{errors{...}}`) |
| Migrate resources | `..._clusterManagement_migrationResourceUpdate(input:{request:{resources:[...]}})` | `Boolean_MutationPayload` (just `result`) |
| Migrate nodes | `..._clusterManagement_migrationNodesUpdate(input:{target:Destination})` | same |
| Restart a node | `..._upgradeNode(input:{address:"akka.tcp://..."})` | `MutationResult_System_Boolean__MutationPayload` (`result{result}`) |
| Finish | `..._clusterManagement_migrationFinish(input:{})` | `Boolean_MutationPayload` |
| Cancel | `..._clusterManagement_migrationCancel(input:{})` | same |

Two GraphQL ↔ JSON quirks worth knowing:

- **Output schema renames `id` → `_id`** on all sub-API types (Relay collision; PR #19 fix). The **input** types still use plain `id`. So when you query a configuration's packages you get `{_id, version}`, but you must send `{id, version}` back via `configurations_update`.
- **Send settings via GraphQL variables, not inline literals.** The resolver coerces variables fine but rejects inline literals like `priority:1.0` (Float → non-nullable `double`) and `forceUpdate:false` (Bool → non-nullable `bool`) with a generic `ARGUMENT` error. `LocalDevClusterUpdate` always passes settings as a typed variable.

## 6. Pitfalls

### Migration stuck in `Preparing`
The chosen target Configuration's `PackagesToInstall` doesn't have the runtime's framework key (`.NETCoreApp,Version=v9.0`). Causes: it was Check'd against an old `SupportedFrameworks` list (pre-PR #20), or `MigrationActor` can't extract the migrator template service.

`LocalDevClusterUpdate` always runs `configurations_check` against the live cluster (so the framework keys match what `MigrationActor` will look up) before `setReady`. If you took the manual path and the configuration was Check'd before PR #20, cancel the migration and re-run the target — it'll create a fresh Draft.

### `seed` container exits, `manager` grabs `172.18.0.6`
The cluster seed image's process can exit cleanly post-bootstrap. Its reserved IP is freed, and a recreated `manager` (no fixed IP) can take it. Subsequent `up -d seed` then fails with "Address already in use".

```bash
docker stop klusterkite-manager-1
docker compose -p klusterkite up -d seed
docker compose -p klusterkite up -d manager
```

`LocalDevClusterUpdate` does not auto-recover from a missing `seed` — without it the cluster can't form, all GraphQL calls return 503, and the target will throw on the first auth attempt. Bring `seed` back first, then re-run.

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
