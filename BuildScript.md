# Build instructions

The build uses [Cake](https://cakebuild.net/) (`build.cake`). [.NET SDK](https://dotnet.microsoft.com/download) is the only prerequisite — Cake runs as a local tool.

```bash
# Run a build target
dotnet cake build.cake --target=<Target>
```

Environment variables used by push targets:

| Variable | Default | Description |
|---|---|---|
| `NUGET_API_KEY` | _(required for push)_ | API key for the local NuGet server |
| `NUGET_SERVER_URL` | `http://docker:81` | URL of the local cluster NuGet server |

## Targets

Targets fall into two groups: **single-step** targets that do exactly one thing (and require prerequisites to have run first), and **composite** targets that run the full pipeline in the correct order.

Normally you want a composite target.

### Single-step targets

#### Clean
Removes the contents of all `temp/` directories used during the build.

#### PrepareSources
Copies all project sources into `temp/build/src/` and rewrites `<Version>` tags. Depends on **Clean**.  
All assemblies default to version `0.0.0-local` unless `SetVersion` ran first in the same session.

#### SetVersion
Queries the local NuGet server for the latest version of `KlusterKite.Core` and sets the next patch version for subsequent build or pack tasks. Depends on **Clean**.

#### Build
Compiles all non-test projects in Release mode from `temp/build/src/`. Depends on **PrepareSources**.

#### BuildDebug
Compiles all projects (including tests) in Debug mode from `temp/build/src/`. Depends on **PrepareSources**.

#### Nuget
Packs NuGet packages from previously built projects (Release). Non-test packages are collected into `temp/packageOut/` (or `temp/packagePush/` if `SetVersion` ran). Depends on **Build**.

#### Tests
Runs all xUnit test projects (identified by `<IsTest>true</IsTest>` in their `.csproj`). Results written as `.trx` files to `temp/build/tests/`. Depends on **BuildDebug**.

#### DockerBase
Builds the base Docker images (`klusterkite/baseworker`, `klusterkite/baseweb`, `klusterkite/nuget`, `klusterkite/postgres`, `klusterkite/entry`, `klusterkite/vpn`, `klusterkite/elk`, `klusterkite/redis`). No dependencies.

#### DockerContainers
Builds service Docker images (`seed`, `seeder`, `worker`, `manager`, `publisher`, `monitoring-ui`). Depends on **PrepareSources**.

#### CleanDockerImages
Removes dangling (unnamed) Docker images left over from successive builds.

#### RestoreThirdPartyPackages
Resolves all third-party NuGet packages used by the solution (with full transitive closure) and copies their `.nupkg` files to `temp/packageThirdPartyDir/`.

#### PushThirdPartyPackages
Pushes all packages in `temp/packageThirdPartyDir/` to the local NuGet server. Fails on first error. Depends on **RestoreThirdPartyPackages**.

#### RePushThirdPartyPackages
Same as `PushThirdPartyPackages` but pushes one package at a time and continues on error. Standalone (no dependency on RestoreThirdPartyPackages).

#### PushLocalPackages
Pushes all packages from `temp/packagePush/` to the local NuGet server one by one, continuing on error. Depends on **Nuget**.

#### RePushLocalPackages
Re-pushes packages from `temp/packagePush/` to the local NuGet server without rebuilding. Standalone.

### Composite targets

#### FinalBuild
Full clean build in Release mode.  
Pipeline: `Clean` → `PrepareSources` → `Build`

#### FinalPushLocalPackages
Builds, versions, packs, and pushes local packages to the NuGet server.  
Pipeline: `Clean` → `SetVersion` → `PrepareSources` → `Build` → `Nuget` → `PushLocalPackages`

#### FinalPushThirdPartyPackages
Resolves and pushes all third-party packages to the NuGet server.  
Pipeline: `RestoreThirdPartyPackages` → `PushThirdPartyPackages`

#### FinalPushAllPackages
Runs both `FinalPushLocalPackages` and `FinalPushThirdPartyPackages`. Use this on a fresh cluster or after updating third-party dependencies.

#### FinalBuildDocker
Builds all Docker images and removes dangling images.  
Pipeline: `DockerBase` → `DockerContainers` → `CleanDockerImages`
