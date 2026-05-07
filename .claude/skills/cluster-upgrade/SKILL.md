---
name: cluster-upgrade
description: Use when upgrading a running KlusterKite local devcluster after code, HOCON, or Docker-image changes. The idiomatic flow is bump-version → push → drive the cluster through a migration; recreating containers in place is a debug shortcut, not the upgrade mechanism.
---

KlusterKite was designed to upgrade in place via configuration migrations, not by replacing running containers. Each cluster Configuration pins a set of `PackageRequirements` and the resolved `PackagesToInstall` per template. To roll new code:

1. **Bump the package version** so old and new can coexist on the feed.
2. **Build and push** to the local NuGet feed.
3. **Create a new Configuration** that requires the new versions; let `ConfigurationCheckActor` resolve them.
4. **Migrate** the cluster from the current Active configuration to the new one. Nodes drain and re-launch under the new versions one role at a time, with rollback if anything fails.

The `--force-recreate` and DB-wipe paths in the Pitfalls section exist for two narrow cases: cold start before the cluster is seeded, and developer-side iteration on a single package version (`0.0.0-local`) where you don't want to bump on every save.

## 1. Idiomatic upgrade

### a. Bump version + push

`build.cake`'s `SetVersion` target queries the local NuGet feed for the highest existing patch and increments by one (`0.0.5-local` → `0.0.6-local`). The composite target that bumps, builds, packs, and pushes in one shot:

```bash
NUGET_API_KEY=KlusterKite NUGET_SERVER_URL=http://localhost:28081 \
  dotnet cake build.cake --target=FinalPushLocalPackages
```

Adjust the port to whatever `Docker/KlusterKite/.env` says (`28081` in this checkout's parameterized layout, `81` upstream).

After the run, the local NuGet feed has the new versioned packages. The currently-running cluster is unaffected — it's still pinned to the old Configuration's `PackagesToInstall`.

If the change includes the **launcher binary itself** (anything under `KlusterKite.NodeManager.Launcher.*` or `KlusterKite.NodeManager.Seeder.Launcher`), you also need new Docker images:

```bash
dotnet cake build.cake --target=FinalBuildDocker
```

### b. Create the new Configuration in the UI

In the monitoring UI (`http://localhost:28082/klusterkite/`) under **Configurations**:

1. Open the current Active configuration.
2. Click **Create a new configuration** (clones settings; new state is Draft).
3. Bump the major / minor as appropriate.
4. Click **Update all packages to the latest version** — `ConfigurationCheckActor` re-resolves every `PackageRequirement` against the live NuGet feed. This is where the new versions get pulled in.
5. **Check configuration** — runs `ConfigurationExtensions.CheckAll`: validates each declared package exists, walks transitive deps, populates `PackagesToInstall` per `SupportedFrameworks` (currently just `.NETCoreApp,Version=v9.0`).
6. **Prepare for publication** — state moves Draft → Ready.

If Check or Prepare reports errors, the new versions aren't fully on NuGet yet, or a transitive dep can't be resolved. Inspect the error before continuing.

### c. Migrate

On the Ready configuration page click **Start migration**. `MigrationActor` extracts service binaries for the new migrator template, runs the cluster compatibility check (Preparing → Ready), and the UI moves to the Migration page. Walk it through:

- **PreNodesResourcesUpdating** — resources flagged for pre-node migration get migrated. Click **Upgrade selected** when there are checked rows.
- **NodesUpdating** — node templates restart under the new packages. Watch the Nodes table; obsolete nodes get torn down and recreated. Use the per-node **Reset** button on the home page if a straggler doesn't pick up the new config.
- **PostNodesResourcesUpdating** — resources flagged for post-node migration.
- **Finish** — once `canFinishMigration` is true, click **Finish migration**. Old config → Obsolete, new config → Active.

If anything fails mid-migration, **Cancel migration** rolls every node back to the previous Active. (Cancel is hidden in the UI when `migrationSteps` is null — i.e. while the migration is still in `Preparing`. The mutation works regardless: see Pitfalls below.)

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
  -d '{"query":"{api{klusterKiteNodesApi{configurations(filter:{state:Active},limit:1){edges{node{name,majorVersion,minorVersion}}}}}}"}' \
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

# 3. Recreate consumers so they re-download. Map: which roles host the package.
#    (manager, worker run providers; publishers run GraphQL.Publisher; seeder runs the seed dll.)
cd Docker/KlusterKite
docker compose -p klusterkite up -d --force-recreate <services...>
```

The recreate cascades to `seeder` (`service_completed_successfully` dep). After PR #20 the seeder will refresh Active and Ready configs against the new NuGet state, which keeps things consistent — but that's a safety net, not the upgrade path.

## 4. Pitfalls

### Migration stuck in `Preparing`
The chosen target Configuration's `PackagesToInstall` doesn't have the runtime's framework key (e.g. `.NETCoreApp,Version=v9.0`). Causes: it was Check'd against an old `SupportedFrameworks` list, or `MigrationActor` can't extract the migrator template service.

Confirm by querying the migrator template's resolved framework keys:

```bash
docker exec klusterkite-configDb-1 sh -c "su postgres -c '
psql \"KlusterKite.NodeManagerConfiguration\" -tAc \"select \\\"SettingsJson\\\" from \\\"Configurations\\\" where \\\"State\\\"=1\"'" \
  | python -c "import json,sys; d=json.load(sys.stdin); [print(t['Code'], list((t.get('PackagesToInstall') or {}).keys())) for t in d.get('MigratorTemplates', [])]"
```

If the framework key is wrong, cancel the migration via API (UI hides the button while `migrationSteps` is null):

```bash
curl -s -X POST -H 'Content-Type: application/json' -H "Authorization: Bearer $TOKEN" \
  -d '{"query":"mutation{klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_migrationCancel(input:{clientMutationId:\"x\"}){result,clientMutationId}}"}' \
  http://localhost:28080/api/1.x/graphQL
```

After cancel, recreate the seeder so PR #20's refresh repopulates `PackagesToInstall` for the Ready candidate. Then re-run Start migration.

### `seed` container exits, `manager` grabs `172.18.0.6`
The cluster seed image's process can exit cleanly post-bootstrap. Its reserved IP is freed, and a recreated `manager` (no fixed IP) can take it. Subsequent `up -d seed` then fails with "Address already in use".

```bash
docker stop klusterkite-manager-1
docker compose -p klusterkite up -d seed
docker compose -p klusterkite up -d manager
```

### nuget container nginx doesn't bind on cold start
`klusterkite-nuget-1` runs hhvm + nginx via supervisord. Nginx fails to bind `:80` on first start. `ss -tln` inside shows only `:9000`. Reload fixes it:

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

## 5. Decision quick-reference

| Change | Use this path |
|---|---|
| Code in any package, idiomatic | §1 — bump version, push, migrate |
| One package, debugging an iteration | §3 — overwrite `0.0.0-local` and recreate consumers |
| Fresh checkout, no Active config in DB | §2 — cold start (push packages, let seeder run) |
| Launcher binary (`KlusterKite.NodeManager.Launcher.*`) | `cake FinalBuildDocker`, then either §1 (preferred) or §3 |
| HOCON inside `KlusterKite.NodeManager/Resources/akka.hocon` | Same as code change to that package |
| HOCON copied into a Docker image (`Docker/KlusterKite*/seeder.hocon` etc.) | `docker buildx build` of that dir, then recreate |
| `docker-compose.yml` | `docker compose up -d --force-recreate <svc>`, no build |
| React app | `npm run build` in `klusterkite-web` → image build → recreate `monitoringUI` |
