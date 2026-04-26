# KlusterKite.NodeManager

Cluster configuration and orchestration, remote node configuration, managing, and updating.
  
## Aim

We have some *system* that is located on a bunch of servers/VMs/Containers (from now on, this documentation will call it **container**). These containers can join and leave the system without the disturbance of the service. We should have an easy way to deploy new features and services, bug fixes to the whole cluster with minimum manual work (let assume that there are a huge amount of containers in our **system**. We have a lot of **nodes** and resources. The new containers should be easily introduced into the cluster. We should have an ability to quickly reconfigure any node, to redistribute roles among containers if needed.

Some of the containers are persistent (that holds the DB, storage data, endpoints, e.t.c), some are not and should be easily added and removed to scale performance / reduce hosting cost according to current **system** load.

## Glossary

* **System** - the application in the broadest sense (including DBMS, web-sites e.t.c.)
* **Node** - the server application node that paticipates in Akka.NET cluster
* **Resource** - the external (from Akka.NET cluster point of view) part of an application (like DB, web-site, e.t.c) that should be updated with the .net code synchroniously.

## Node container configuration

To store all executed code **KlusterKite** based **system** should have a private NuGet server as part of the cluster. It is used to store and distribute code across all nodes that are going to join the cluster. The malfunction of NuGet server will not halt the **system** work but will prevent the new nodes start.

Each container, intended to run some of the **systems** code should have a preinstalled **KlusterKite.NodeManager.Launcher** service, that should start on container start. This service is rather lightweight and is supposed to be updated very rarely. It's the only purpose to request the node configuration from the **system**, download and extract needed packages from the NuGet server, create the [`KlusterKite.Core.Service`](../KlusterKite.Core/Readme.md), add the top-level configuration and launch it. In the case of the service stop - it restarts the whole cycle from the beginning. This service has some configuration parameters that are stored in `config.hocon`:
* `NodeManagerUrl` - the endpoint (URL) of `KlusterKite.NodeManager` configuration API
* `authenticationUrl` - the endpoint to authenticate in **system** to access API
* `apiClientId` and `apiClientSecret` - the authentication credentials to authenticate in **system** to access API
* `runtime` - the description of the container runtime (see [RID](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog))
* `containerType` - the symbolic description of the current container type. Not all containers are identical. The can have different hardware parameters or different preinstalled third-party software or else. The received configuration depends on container type
* `fallbackConfiguration` - the path to the fallback configuration file (in JSON serialized format) that will be used in case of whole **system** down. That is used only on global **system** start-up. This configuration is also embedded in the container.

In order to make things work (to distribute configurations to the starting **nodes**), there should be always some working **node** with `KlusterKite.NodeManager` plugin that is correctly published to the **system** endpoint (see [`KlusterKite.Web`](../KlusterKite.Web/Readme.md))

## Cluster `Configuration` and `Migrations`

### Node template
In order to define the configuration that is sended to `KlusterKite.NodeManager.Launcher` **KlusterKite** introduces the [`NodeTemplate`](../Docs/Doxygen/html/class_kluster_kite_1_1_node_manager_1_1_client_1_1_o_r_m_1_1_node_template.html) entity.

When `NodeManager` receives a new configuration request it selects the template in following order (the template should have the `containerType` among it's `ContainerTypes`):
1. If there are templates with less active nodes then `MinimumRequiredInstances` it will apply one of them in the `Priority` order (from highest to lowest)
2. If there are templates with less active nodes then `MaximumNeededInstances` (or `MaximumNeededInstances` is `null`) it will apply one of them in the `Priority` order (from highest to lowest)
3. Otherwise, it will send a special signal, so none of the templates will be applied and `KlusterKite.NodeManager.Launcher` will wait for some time to repeat the request.

The node template (aka the node configuration) includes the following information:
* The list of NuGet packages (along with their exact versions) to be installed
* The top-level configuration (that overrides any parameter from plugins default configuration)

## Cluster Configuration
The list of all **Node templates** along with some other parameters is called **Cluster Configuration** or just [`Configuration`](../Docs/Doxygen/html/class_kluster_kite_1_1_node_manager_1_1_client_1_1_o_r_m_1_1_configuration.html).

The special parameters are:
* **Packages** - the list of all (with direct or indirect references) used NuGet packages and their versions. `NodeTemplate` defines only the list of plugin packages (with optional version, if omitted the version from configuration packages list will be used) and optionally special packages and their version if they are not specified or another version in cluster configuration packages list
* **SeedAddresses** - the list of Akka.NET Cluster seed nodes that are used as Cluster Seeds (or lighthouse) to let the new node join Akka.NET Cluster. Please check the Akka.NET Cluster documentation.
* **NugetFeed** - the address of the **system** NuGet server to acquire the packages
* **Migrator templates** - the migrator templates are described below

## Migrations

There can be any number of defined **configurations**, but only one can be used at a time (the one that has `Active` state). The *Active* configuration cannot be changed and is immutabel. The process of switching from one configuration to another is called: [`Migration`](../Docs/Doxygen/html/class_kluster_kite_1_1_node_manager_1_1_client_1_1_o_r_m_1_1_migration.html).

But during the migration process, not only **nodes** are needed to be upgraded, but also **resources**. Some of them, like DB schemas, are needed to be updated before **nodes** (`CodeDependsOnResource` dependence type), others - like web sites that use the **system** API - after the **nodes** (`ResourceDependsOnCode` dependence type). And if the **system** has a large amount of **resources** it is hard to make adjustments manually and it is more reliable to have this adjustment to be scripted and distributed among all code so the developers can be sure that **resources** and **nodes** are of the same version.

To provide the automation of this processes there are [`MigratorTemplates`](../Docs/Doxygen/html/class_kluster_kite_1_1_node_manager_1_1_client_1_1_o_r_m_1_1_migrator_template.html) and **Migrators** entities.

The **MigratorTemplate** is much alike **NodeTemplate** and defined in a similiar way in the configuration. `KlusterKite.NodeManager` has a cluster singletone that assembles and launches the `KlusterKite.NodeManager.Migrator.Executor` service assembled based on `MigratorTemplates` configuration. The top-level configuration of the template should have `KlusterKite.NodeManager.Migrators` string array that contains the list of type names of **Migrators** to be executed. The **Migrator** is a class that implements [`IMigrator`](../Docs/Doxygen/html/interface_kluster_kite_1_1_node_manager_1_1_migrator_1_1_i_migrator.html) interface.

The resource migration model was copied from Entity Framework Code-First migrations. The resource should have some chronological states (called **migration points**) and **migrator** should be able to change states from one to another. It is assumed, that **migrator** should be able to revers changes to any state in the past and can upgrade the resource from any past state to current state.

If there are no active migrations, `KlusterKite.NodeManager` will launch all defined `MigratorTemplates` and their migrators to assure that all defined resources are existing and in the state of last defined migration point. If everything is ok the new migration can be created.

After migration is created the `MigratorTemplates` and their migrators are executed for both old and new configurations to check the resource changes. If the list of migration points for some **Migrator** of new configuration starts with all points of old configuration and have some new one - it is considered as resource upgrade. If the list of migration points for some **Migrator** of old configuration starts with all points of the new configuration and have some extra points - it is considered as resource downgrade (it can happen in the case of system update rollback, when the previous version is installed). 

The migration is executed in the following steps:
1. All upgrading or creating resources with `CodeDependsOnResource` dependence type and all downgrading resources with `ResourceDependsOnCode` dependence type should be adjusted. In the case of butch resource migration, the resources are migrated in following order: the downgraded resources are migrated first, then resources are migrated in the **Migrator** priority order (`asc` for downgrade and `desc` for upgrade).
2. All nodes should be adjusted. This process is performed automatically. Only those node that has changes in packages definitions and/or configuration will be updated. During the update process the `KlusterKite.NodeManager` will assure that there will be no moment when the **system** will have less active nodes of `NodeTemplate` that is defined in `MinimumRequiredInstances` to maintain the zero time **system** work interruption.
3. All upgrading or creating resources with `ResourceDependsOnCode` dependence type and all downgrading resources with `CodeDependsOnResource` dependence type should be adjusted. In the case of butch resource migration, the resources are migrated in following order: the downgraded resources are migrated first, then resources are migrated in the **Migrator** priority order (`asc` for downgrade and `desc` for upgrade).

The migration step execution is controlled via API or UI.
There is UI that provides access to the `KlusterKite.NodeManager` API. Please check the sample [`Docker`](../Docker/Readme.md) documentation.

## Node management lifecycle

Every cluster member is brought up, kept obsolescence-checked, and recycled by the singleton `NodeManagerActor` (`KlusterKite.NodeManager/NodeManagerActor.cs`). The lifecycle is the same for every node, regardless of role:

1. **Container start.** The `KlusterKite.NodeManager.Launcher` boots, reads its `config.hocon`, and asks the cluster API for a configuration via `NewNodeTemplateRequest` (carrying `containerType` and `frameworkRuntimeType`).
2. **Template selection.** `NodeManagerActor.OnNewNodeTemplateRequest` calls `GetPossibleTemplatesForContainer`, applies the **MinimumRequiredInstances → MaximumNeededInstances** priority rules described above, and replies with a `NodeStartUpConfiguration` that includes the resolved package list, HOCON overrides, NuGet feed, and seed addresses. If no template fits, the launcher receives a `NodeStartupWaitMessage` and retries after `KlusterKite.NodeManager.FullClusterWaitTimeout` (default 60s).
3. **Pending registration.** The chosen request is appended to `awaitingRequestsByTemplate[templateCode]` so subsequent template selection counts the not-yet-joined node. After `KlusterKite.NodeManager.NewNodeJoinTimeout` (default 30s) the entry is dropped if the node never appears.
4. **Cluster join.** The launcher downloads the packages, starts `KlusterKite.Core.Service`, and the new process joins the Akka cluster. The manager observes `ClusterEvent.MemberUp` and starts polling the new node with `RequestDescriptionNotification` every `NewNodeRequestDescriptionNotificationTimeout` (default 10s) up to `NewNodeRequestDescriptionNotificationMaxRequests` times (default 10).
5. **Description received.** The new node responds with a `NodeDescription`. The manager stores it, removes it from `awaitingRequests`, and runs `CheckNodeIsObsolete` against the active configuration.
6. **Obsolete check.** A node is marked `IsObsolete = true` when `nodeDescription.ConfigurationId != currentConfiguration.Id` *and* there is no entry in `currentConfiguration.CompatibleTemplatesBackward` whose `TemplateCode` and `CompatibleConfigurationId` match the node. Compatible templates are computed in `ConfigurationExtensions.GetCompatibleTemplates` — a template is considered compatible only if its HOCON `Configuration`, `PackageRequirements` set, and resolved package versions match the previous configuration.
7. **Reconciliation tick.** After every node description, `MemberUp`, `MemberRemoved`, or migration step transition, the manager schedules an `UpgradeMessage` that runs `OnNodeUpgrade` (see *Automatic node upgrade* below).
8. **Shutdown.** When the manager wants to recycle a node it sends a `ShutdownMessage` to `/user/NodeManager/Receiver` on that node. `KlusterKite.Core.Service` exits gracefully, the launcher restarts it, and the node re-enters step 1 with the latest configuration.

## Automatic node upgrade

`NodeManagerActor.OnNodeUpgrade` is the only place where automatic node recycling happens. It runs on every `UpgradeMessage` (received after a description update, after a migration step transition, and rescheduled while an upgrade is in flight).

For each `NodeTemplate` group of currently registered nodes:

* If no node in the group is `IsObsolete`, the group is skipped.
* If the group's live count is **less than or equal to** `MinimumRequiredInstances`, **no node is recycled**. This is the primary safety guard — the manager refuses to take a template below its declared minimum, even if every node is obsolete. New replacement nodes must come up first, which only happens once they request a template (see template selection rules) and join the cluster.
* Otherwise the manager picks `ceil(groupCount * upgradablePart / 100) - nodesAlreadyUpgrading` of the obsolete nodes (oldest by `StartTimeStamp` first), records them in `upgradingNodes`, and sends each one a `ShutdownMessage` via `OnNodeUpdateRequest`. `upgradablePart` comes from `KlusterKite.NodeManager.NewNodeRequestDescriptionNotificationMaxRequests` (yes, the field is named after a different setting in code; the default is 10, i.e. 10% of the group at a time).
* An entry in `upgradingNodes` is considered stale and dropped after `NewNodeJoinTimeout + NewNodeRequestDescriptionNotificationTimeout`, so a lost recycle attempt does not stall progress forever.
* After a successful pass the actor schedules another `UpgradeMessage` slightly past that timeout to advance to the next batch.

Net effect: the cluster drains obsolete nodes in waves, never dropping any template below its safe quorum, and never recycling more than `upgradablePart`% of a template at once.

## Migration: states and steps

Two independent state machines drive a migration; both are visible via the `clusterManagement` GraphQL surface.

**`Migration.State`** ([`EnMigrationState`](KlusterKite.NodeManager.Client/ORM/EnMigrationState.cs)):

| State | Meaning |
|-------|---------|
| `Preparing` | The migration row was just created. The `MigrationActor` is collecting resource state from every `MigratorTemplate`. No resource or node action is allowed yet. |
| `Ready` | Resource state was successfully collected; step transitions are now permitted. |
| `Failed` | The migration was canceled; the source configuration is restored as `Active`. |
| `Completed` | The migration was finished; the destination configuration is now `Active`, the previous one becomes `Archived`. |
| `Rollbacked` | Reserved (TODO in code). |

**Step within a `Ready` migration** ([`EnMigrationSteps`](KlusterKite.NodeManager.Client/ORM/EnMigrationSteps.cs)). The current step is computed by `NodeManagerActor.GetCurrentMigrationStep` from three independent observations: where the active node descriptions are (source/destination), where the `pre-node` migratable resources are, and where the `post-node` migratable resources are. The legal step sequence for an upgrade migration is:

1. `Start` — nothing has happened. If there are pre-node resources, they must be migrated first; otherwise nodes can move directly.
2. `PreNodesResourcesUpdating` — the `MigrationActor` is currently rewriting `CodeDependsOnResource` resources to the destination version.
3. `PreNodeResourcesUpdated` — pre-node resources are at destination; nodes can now be flipped.
4. `NodesUpdating` — at least one node is still `IsObsolete`. The manager is recycling them in waves (see *Automatic node upgrade*).
5. `NodesUpdated` — every node is at destination; if there are post-node resources to migrate, they must be done now.
6. `PostNodesResourcesUpdating` — the `MigrationActor` is rewriting `ResourceDependsOnCode` resources.
7. `Finish` — everything is at destination, ready for `migrationFinish`.

Two recovery steps can also appear:

* `Recovery` — nodes and resources are inconsistent in a way that can be reconciled by sending updates in either direction.
* `Broken` — at least one resource is `OutOfScope` (no migrator can reach the desired state) or a migrator's `Direction` is `Undefined`. Manual operator intervention is required; `migrationCancel` is the only legal next operation when conditions allow.

The `ResourceState.Can*` flags returned to the API are set by `InitResourceMigrationState` strictly from the current step:

| Step | `CanMigrateResources` | `CanUpdateNodesToDestination` | `CanUpdateNodesToSource` | `CanCancelMigration` | `CanFinishMigration` |
|------|----------------------|-------------------------------|--------------------------|----------------------|----------------------|
| `Start` | only if pre-node resources exist | only if no pre-node resources | – | yes | – |
| `PreNodesResourcesUpdating` | yes | – | – | – | – |
| `PreNodeResourcesUpdated` | yes | yes | – | – | – |
| `NodesUpdating` | – | nodes still at source | nodes already at destination | – | – |
| `NodesUpdated` | yes | – | yes | – | – |
| `PostNodesResourcesUpdating` | yes | – | – | – | – |
| `Finish` | only if post-node resources exist | – | only if no post-node resources | – | yes |
| `Recovery` | yes | only if nodes not yet at destination | only if nodes not yet at source | – | – |
| `Broken` | – | – | – | only if every resource is at source/notCreated/obsolete | – |

When no migration is active the equivalent `ResourceState.CanCreateMigration` flag is set by `InitResourceConfigurationState`, and is `false` whenever any node is still `IsObsolete` or any resource is not at its `LastDefinedPoint`.

## Triggering and progressing a migration

The migration progresses **only** through explicit API calls — `OnNodeUpgrade` reacts to migration state, but never advances it. All mutations live on the GraphQL `Root.clusterManagement` connection (`KlusterKite.NodeManager/WebApi/ClusterManagement.cs`) and require the `ClusterManagement.MigrateCluster` privilege.

| Mutation | Underlying actor message | When to use |
|----------|--------------------------|-------------|
| `clusterManagement.migrationCreate(newConfigurationId)` | `UpdateClusterRequest` | Start a migration to a non-`Draft` target configuration. The current configuration becomes the migration's source. |
| `clusterManagement.migrationResourceUpdate({ resources: [...] })` | `List<ResourceUpgrade>` | Run one resource through its migrator to source/destination. Allowed only while `CanMigrateResources` is `true`. |
| `clusterManagement.migrationNodesUpdate(target)` | `NodesUpgrade { Target = Source\|Destination }` | Flip the *Active* configuration to source or destination. This is what makes the existing nodes start being recognized as `IsObsolete` and lets `OnNodeUpgrade` recycle them. Allowed only while the corresponding `CanUpdateNodesTo*` flag is `true`. |
| `clusterManagement.migrationCancel()` | `MigrationCancel` | Roll back: marks destination `Faulted`, source `Active`, migration `Failed`. Allowed only while `CanCancelMigration` is `true`. |
| `clusterManagement.migrationFinish()` | `MigrationFinish` | Closes a `Finish`-step migration: source becomes `Archived`, destination stays `Active`. |
| `clusterManagement.recheckState()` | `RecheckState` | Force the manager to reload configuration and resource state from the database — useful after an out-of-band schema fix. |

A typical successful upgrade looks like this:

1. Operator publishes a draft configuration and calls `configurationSetReady` (handled by `ConfigurationCheckActor`).
2. Operator calls `migrationCreate(newId)`. Migration enters `Preparing`, then `Ready`/`Start`.
3. If pre-node resources need updating, operator calls `migrationResourceUpdate` for each (or all). Step advances to `PreNodeResourcesUpdated`.
4. Operator calls `migrationNodesUpdate(Destination)`. The destination configuration becomes `Active`, every existing node is re-evaluated by `CheckNodeIsObsolete` and `OnNodeUpgrade` starts recycling them in waves. Step is `NodesUpdating` until the last obsolete node is replaced.
5. Step advances to `NodesUpdated`. If post-node resources exist, operator calls `migrationResourceUpdate` for them.
6. Operator calls `migrationFinish`. Migration is `Completed`, source is `Archived`.

## Manual single-node upgrade

There is also a per-node "kick" mutation:

```graphql
mutation { upgradeNode(address: "akka.tcp://KlusterKite@1.2.3.4:3090") { result } }
```

It is implemented in `NodeManagerApi.UpgradeNode` (privilege `Privileges.UpgradeNode`) and translates to a `NodeUpgradeRequest` handled by `NodeManagerActor.OnNodeUpdateRequest`, which simply forwards a `ShutdownMessage` to `/user/NodeManager/Receiver` on the target node. The launcher restarts the process, which then re-requests its configuration and rejoins.

This bypass is useful for:

* Forcing a node to pick up a configuration change that the manager considers compatible (and therefore would not recycle automatically).
* Recovering a node that is wedged but still part of the cluster.
* Recycling a node that is below `MinimumRequiredInstances` (use with care — there is no quorum guard on this path).

It does **not** advance migration state, and it does **not** wait for the new node to come up.

## Troubleshooting: nodes are not being upgraded

When a node — or a whole template — refuses to recycle after a migration step, walk through these checks in order:

1. **Is a migration even active?** Query `clusterManagement.currentMigration`. If it is `null`, no automatic recycling will happen — `OnNodeUpgrade` only acts on nodes whose `IsObsolete` flag is `true`, and that flag is only set when the active configuration changed. Without `migrationNodesUpdate(Destination)` having been called, the active configuration is still the source one and nothing is obsolete.
2. **Is the resource pipeline still running?** `clusterManagement.resourceState.operationIsInProgress = true` blocks every `Can*` flag. Wait for the `MigrationActor` to finish the current resource batch, or check the manager's logs for `MigrationActorInitializationFailed` errors that left it stuck.
3. **Is the current step what you think it is?** Inspect `resourceState.currentMigrationStep`. If it is `PreNodesResourcesUpdating` or `PreNodeResourcesUpdated`, you must run `migrationResourceUpdate` for the listed pre-node resources before `migrationNodesUpdate(Destination)` becomes legal.
4. **Was the destination treated as compatible?** Look at `currentConfiguration.compatibleTemplatesBackward` for the affected template. If it contains the node's `(TemplateCode, ConfigurationId)` pair, `CheckNodeIsObsolete` will set `IsObsolete = false` on purpose — the manager believes the existing process is already running compatible code and does not need to be recycled. Common reasons a previous configuration is auto-marked compatible:
    * the template's HOCON `Configuration` is byte-identical;
    * the template's `PackageRequirements` set is identical;
    * every floating package version (`SpecificVersion = null`) resolves to the same version in both configurations' `Packages` list.
   To force a recycle anyway, either change one of those inputs (e.g. set `NodeTemplate.ForceUpdate = true` so the template ignores compatibility), or call the per-node `upgradeNode` mutation.
5. **Is the template at minimum quorum?** If `activeNodesByTemplate[code].Count <= NodeTemplate.MinimumRequiredInstances`, `OnNodeUpgrade` skips the group entirely. Either lower `MinimumRequiredInstances`, or scale the cluster up by adding containers of the matching `containerType` so the manager has room to recycle one without dropping below the floor.
6. **Are replacement nodes able to start?** Recycling depends on new nodes being able to claim the destination template via `NewNodeTemplateRequest`. Check the manager log for `Cluster is full` or `There is no configuration available for {ContainerType}` warnings — they mean the template's `MaximumNeededInstances` is already saturated by `awaitingRequests` (still booting) or that no `containerType` matches.
7. **Is `upgradablePart` saturated?** While a wave is in flight, `upgradingNodes` is non-empty and the next wave waits until each in-flight upgrade either acknowledges (`OnNodeDescription` clears it) or times out after `NewNodeJoinTimeout + NewNodeRequestDescriptionNotificationTimeout`. If many nodes time out repeatedly, the new node containers are failing to start — inspect the launcher logs on the recycled hosts.
8. **Is the migration `Broken` or `Recovery`?** No automatic node movement happens in either step. For `Recovery` you can drive nodes back to source or forward to destination manually with `migrationNodesUpdate`; for `Broken` only `migrationCancel` is allowed (and only when every resource is at source/notCreated/obsolete).
9. **Did the database go stale?** If the manager's view diverges from the database (e.g. after a manual SQL fix or after restoring a backup), call `clusterManagement.recheckState()` to force `InitDatabase` and a fresh `RecheckState` round-trip to the `MigrationActor`.

If, after these checks, the cluster still will not recycle a specific node, fall back to the manual single-node upgrade described above.

## Seeders

In order to provide easy sandbox start-up, KlusterKite has **seeder** function to create the resources from the scratch. The sandbox should have a container with preinstalled and configured `KlusterKite.NodeManager.Seeder.Launcher` utility. This utility will read it's configuration and start the specified **Seeders** (that inherits the [`BaseSeeder`](../Docs/Doxygen/html/class_kluster_kite_1_1_node_manager_1_1_migrator_1_1_base_seeder.html) class). Every seeder should check for resource pre-existence to avoid generating errors in case of a subsequent run.

Please check the [`Docker`](../Docker/Readme.md) example of the confiugured seeder.