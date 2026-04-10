# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KlusterKite is a C#/.NET 9.0 framework for building scalable and redundant distributed services on top of [Akka.NET](https://github.com/akkadotnet/akka.net). It abstracts cluster management, plugin loading, API publishing, authentication, data persistence, and monitoring into a plugin-based architecture.

## Build System

The build uses [Cake](https://cakebuild.net/) (`build.cake`). The .NET SDK is the only prerequisite.

```bash
# Run a build target
dotnet cake build.cake --target=<Target>
```

Push targets require `NUGET_API_KEY` and optionally `NUGET_SERVER_URL` (default: `http://docker:81`).

### Key build targets

| Target | Description |
|--------|-------------|
| `FinalBuild` | Clean → PrepareSources → Build (Release) |
| `Build` | Compile all projects in Release mode (requires PrepareSources) |
| `BuildDebug` | Compile all projects in Debug mode (requires PrepareSources) |
| `Tests` | BuildDebug → run all xUnit test projects |
| `Nuget` | Build → pack NuGet packages to `temp/packageOut/` |
| `FinalBuildDocker` | DockerBase → DockerContainers → CleanDockerImages |
| `FinalPushAllPackages` | Full pipeline: version bump, build, pack, push local + third-party packages |

**Important:** Build targets copy sources to `temp/build/src/` and replace `<Version>` tags before compiling. Do not edit files in `temp/` directly.

### Running tests directly (without Cake)

Test projects are identified by `<IsTest>true</IsTest>` in their `.csproj`. To run a single test project without the full build pipeline:

```bash
# Build debug first (or use dotnet build directly)
dotnet build <ProjectPath> --configuration Debug

# Run a specific test project
dotnet test <TestProject.csproj> --no-build --logger:trx

# Run a single test class or method
dotnet test <TestProject.csproj> --filter "FullyQualifiedName~ClassName"
```

Test results are written as `.trx` files to `temp/build/tests/`.

### Version management

Default version is `0.0.0-local`. The `SetVersion` target queries a local NuGet server at `http://docker:81` and auto-increments the patch version. `NUGET_API_KEY` environment variable is required for push operations.

## Architecture

### Plugin system

Every module exposes a `BaseInstaller` (from `KlusterKite.Core`) that registers Autofac dependencies and merges HOCON configuration. Installers have two priority tiers:

- `PrioritySharedLib` — shared message definitions and utilities
- `PriorityClusterRole` — end-user role implementations

The actor system is initialized by collecting all `BaseInstaller` implementations in the loaded assemblies, merging their HOCON configurations in priority order, then starting Akka.NET.

### HOCON configuration

Each module embeds its Akka configuration in `Resources/akka.hocon`. The `NameSpaceActor` reads the `akka.actor.deployment` section to create hierarchical actor trees declaratively. Configuration from higher-priority installers overrides lower-priority ones.

### Actor creation patterns

Actors are created through Autofac DI. The framework supports four deployment styles declared in HOCON:
- Plain actors
- Cluster singletons
- Singleton proxies
- Sharded actors (via Akka.Cluster.Sharding)

### Module responsibilities

| Module | Purpose |
|--------|---------|
| `KlusterKite.Core` | Actor system bootstrap, plugin discovery, `NameSpaceActor` |
| `KlusterKite.Core.Service` | Executable host entry point |
| `KlusterKite.Core.TestKit` | `BaseActorTest<TConfigurator>` base class for actor tests |
| `KlusterKite.API` | REST API attribute-based definition, client code generation |
| `KlusterKite.Web` | HTTP integration, GraphQL publisher, Nginx configurator |
| `KlusterKite.Web.Authentication/Authorization` | Auth abstractions for external clients |
| `KlusterKite.Data` | Persistence abstractions, CRUD generic actors |
| `KlusterKite.Data.EF` | Entity Framework base; `.InMemory` and `.Npgsql` providers |
| `KlusterKite.Security` | Auth/authz abstractions shared across modules |
| `KlusterKite.Log` | Serilog configuration and centralized log routing |
| `KlusterKite.NodeManager` | Cluster orchestration, remote node config, rolling updates |
| `KlusterKite.Monitoring` | Diagnostics collection from cluster nodes |
| `KlusterKite.LargeObjects` | Out-of-band messaging for payloads that exceed Akka message size limits |

### Testing pattern

Tests inherit from `KlusterKite.Core.TestKit.BaseActorTest<TConfigurator>` where `TConfigurator` customizes the `ActorSystem` setup. Plugin installers are reused in tests to wire up the same DI as production.

## Key files

- `common.props` — shared NuGet package metadata (version, authors, URLs) imported by all `.csproj` files
- `KlusterKite.sln` — solution root; all projects are referenced here
- `KlusterKite.sln.DotSettings` — ReSharper/Rider code style settings; follow these conventions
- Each module's `Resources/akka.hocon` — HOCON actor deployment config embedded in the assembly

## Docker

Base images (`klusterkite/baseworker`, `klusterkite/baseweb`, `klusterkite/postgres`, `klusterkite/elk`, `klusterkite/redis`, etc.) are built via the `DockerBase` target. Service images (`seed`, `seeder`, `worker`, `manager`, `publisher`) are built via `DockerContainers`. The local cluster NuGet server runs at `http://docker:81`.