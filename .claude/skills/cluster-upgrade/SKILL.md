---
name: cluster-upgrade
description: Use when upgrading a running KlusterKite local devcluster after code, HOCON, or Docker-image changes. Covers picking the right rebuild scope, force-pushing packages over `0.0.0-local`, restarting only the affected containers, and avoiding the boot-time landmines (seeder cold-start race, stale Active config, nuget nginx not binding, seed-IP collision).
---

KlusterKite has a launcher-baked-into-Docker-image plus packages-downloaded-from-nuget-at-runtime architecture. After a code change you almost never want to rebuild every Docker image — the right scope and order saves 5-15 minutes per iteration. Apply the checks in order.

## 0. Always first: which cluster, what's running

```bash
docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' | head -20
cat Docker/KlusterKite/.env 2>/dev/null      # local port overrides (if any)
```

Project name defaults to the compose dir's basename. Containers are `<project>-<service>-1`. The user may have a parallel cluster on another project name (e.g. `forge-*`) using the same `klusterkite/*` images. Don't restart its containers.

## 1. Decide the rebuild scope

| Change | Scope | Reason |
|---|---|---|
| C# in a single package (`KlusterKite.X`) | Cake `Nuget` → push that package → recreate consumers | Launcher re-downloads packages on every container start. Image stays. |
| C# in `KlusterKite.NodeManager.Launcher.*` (the binary baked into images) | Cake `DockerContainers` → recreate every cluster service | Launcher itself is the binary; package only is not enough. |
| HOCON embedded in a package (e.g. `Resources/akka.hocon`) | Same as code change to that package | Resource is compiled in; rebuilding the package picks it up. |
| HOCON in `Docker/KlusterKite*/seeder.hocon` etc. | `docker buildx build` of that image dir → recreate consumers | These files are `COPY`'d in; rebuilding the package does NOT touch them. |
| Dockerfile / base image | Cake `DockerBase` (rare) or `DockerContainers` | |
| `Docker/KlusterKite/docker-compose.yml` | `docker compose -p klusterkite up -d --force-recreate <svc>` | No build needed. |
| React app | `npm run build` in `Docker/KlusterKiteMonitoring/klusterkite-web` → `docker buildx build -t klusterkite/monitoring-ui:latest Docker/KlusterKiteMonitoring` → recreate `monitoringUI` | |
| `schema.json` regenerated from a running cluster | Treat as React change (npm run build picks up the new schema for babel-relay-plugin) | |

## 2. Build + push the package(s)

```bash
NUGET_API_KEY=KlusterKite NUGET_SERVER_URL=http://localhost:28081 \
  dotnet cake build.cake --target=Nuget
```

(Adjust port to whatever the local `.env` says.)

The built `.nupkg` lands in `temp/packageOut/`.

### Force-push over `0.0.0-local`

`simple-nuget-server` returns **409 Conflict** when a version already exists. Wipe both the DB row and the file BEFORE pushing:

```bash
PKG_ID=KlusterKite.API.Provider          # ← only the changed packages
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
```

If MSYS rewrites `/var/www/...` to a Windows path, prepend `MSYS_NO_PATHCONV=1`.

## 3. Recreate the right containers

Look up the **role** of each modified package (which template includes it as a `PackageRequirement`) and restart that role:

| Modified package family | Containers to recreate |
|---|---|
| `KlusterKite.API.Provider`, `KlusterKite.Web.GraphQL.Publisher` | `publisher1 publisher2 manager worker` |
| `KlusterKite.NodeManager` (actor code) | `manager worker` |
| `KlusterKite.NodeManager.ConfigurationSource.Seeder` | `seeder` |
| `KlusterKite.NodeManager.Launcher.Utils` (used at runtime by the launcher's package install path) | All cluster services. Launcher binary already has its own copy from image-build time; the cluster-side copy gets refreshed by recreate. |
| HOCON resource changes inside `KlusterKite.NodeManager` | `manager worker` (manager loads the resource) |
| Anything affecting Configuration shape | After the recreate above, also force-recreate `seeder` so it can refresh Active/Ready configs against the new `SupportedFrameworks` / package set. |

```bash
cd Docker/KlusterKite
docker compose -p klusterkite up -d --force-recreate publisher1 publisher2 manager worker
```

`docker compose` will cascade `seeder` (`service_completed_successfully` dep) automatically. Watch the seeder log:

```bash
docker logs --since=2m klusterkite-seeder-1 | grep -E 'Refreshed|Cannot seed|Cannot refresh|Seeder stopped'
```

You want `Seeder stopped with exit code 0` and one `Refreshed configuration #N` per Active/Ready config.

## 4. Verify

```bash
# Auth round-trip (proves manager + publishers + entry are talking)
curl -s --max-time 8 -X POST -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password&username=admin&password=admin&client_id=KlusterKite.NodeManager.WebApplication' \
  http://localhost:28080/api/1.x/security/token

# GraphQL introspection (proves publishers loaded their providers)
curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"query":"{__schema{queryType{name}}}"}' \
  http://localhost:28080/api/1.x/graphQL
```

If the publisher container has the right Akka version (otherwise it crashes with `Could not load Akka, Version=X.Y.Z`):

```bash
docker exec klusterkite-publisher1-1 ls -la /opt/klusterkite/node/service/Akka.dll
# 946176 bytes ≈ Akka 1.5.67; 931840 ≈ 1.5.45
```

## Pitfalls

### Nuget container nginx doesn't bind on cold start
`klusterkite-nuget-1` starts hhvm + nginx via supervisord, but nginx fails to bind to `:80` on first start. `ss -tln` inside shows only `:9000`. A reload fixes it:

```bash
docker exec klusterkite-nuget-1 nginx -s reload
```

Symptom: seeder logs `Connection refused (nuget:80)` in a retry loop.

### `seed` container exits cleanly, then can't restart
The `klusterkite/seed` image's process can exit with 0 after bootstrap. The container's reserved IP (`172.18.0.6` in `docker-compose.yml`) is then free, and a recreated `manager` may grab it. The next `docker compose up -d seed` then fails with `Bind for 0.0.0.0:0/tcp failed: Address already in use`.

Workaround when seed is exited:
```bash
docker stop klusterkite-manager-1
docker compose -p klusterkite up -d seed
docker compose -p klusterkite up -d manager
```

### Stale `PackagesToInstall` after a package version bump
Symptom on node restart:
```
Unhandled exception. System.IO.FileNotFoundException:
Could not load file or assembly 'Akka, Version=X.Y.Z, ...'.
```

Cause: a prior seeder run resolved package versions against an older NuGet feed and persisted them in the DB. After PR #20 the seeder refreshes Active+Ready configs on every run — just `up -d --force-recreate seeder` and check its log shows `Refreshed configuration #N`. If for some reason that's not enough (e.g. ConfigurationCheckActor still in memory holding old refs), also recreate `manager`.

### Migration stuck in `Preparing`
Cause: the configuration's `PackagesToInstall` lacks the framework key the runtime needs (`.NETCoreApp,Version=v9.0`). After PR #20 the seeder writes only `v9.0`, but a Configuration created by the UI BEFORE that fix landed will still be wrong. Fix via:
1. Cancel the migration: `mutation { klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_migrationCancel(input:{clientMutationId:"x"}){result,clientMutationId} }`. Cancel is hidden in the UI when `migrationSteps` is null (Preparing state); the API works.
2. After the seeder's next refresh (re-run seeder), Release N will have the v9 key, and Start migration will go past Preparing.

### Package wipe didn't survive a nuget container restart
The simple-nuget-server's sqlite + filesystem are in volumes that persist. If you wiped a `versions` row but didn't also delete `/var/www/packagefiles/<pkg>/<ver>.nupkg`, the next push still 409s. Always do BOTH steps in §2.

### `dotnet nuget push` rejects HTTP
PR #17 added `--allow-insecure-connections` to every Cake push site. If you're pushing manually, include the flag.

### MonitoringUI shows stale UI after rebuild
Browser caches the bundled JS aggressively. Hard-reload (Ctrl/Cmd-Shift-R). The bundled JS filename hash changes on every successful build, so cached URLs return 404 after a refresh — that's the signal the new bundle is live.

### Two clusters fighting for ports / IPs
The `forge-*` cluster and the `klusterkite-*` cluster share `klusterkite/*:latest` images but use different Docker networks (172.18.x.x vs 172.19.x.x) and different host ports (`Docker/KlusterKite/.env` overrides 80→28080 / 81→28081 / 82→28082 / 9200→28200 / 5601→28601 / 1194→28194). A `docker compose down` in one project won't kill the other. Restarts that recreate `klusterkite/*:latest` images affect both clusters' next container restart, but already-running containers keep their pinned image ID.

## Quick reference: what I touched in this run

```
NUGET_API_KEY=KlusterKite NUGET_SERVER_URL=http://localhost:28081 \
  dotnet cake build.cake --target=Nuget        # builds → temp/packageOut/

# wipe + push specific packages (script all the IDs you changed)
# recreate consumers
cd Docker/KlusterKite
docker compose -p klusterkite up -d --force-recreate <services...>

# wait
until curl -fs http://localhost:28080/api/1.x/security/token \
  -X POST -d 'grant_type=password&username=admin&password=admin&client_id=KlusterKite.NodeManager.WebApplication' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -o /dev/null; do sleep 5; done
```
