// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeManagerApi.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   The node manager api
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.NodeManager.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Akka.Actor;

    using ClusterKit.API.Attributes;
    using ClusterKit.API.Attributes.Authorization;
    using ClusterKit.API.Client;
    using ClusterKit.API.Client.Converters;
    using ClusterKit.Core;
    using ClusterKit.Data.CRUD;
    using ClusterKit.NodeManager.Client;
    using ClusterKit.NodeManager.Client.ApiSurrogates;
    using ClusterKit.NodeManager.Client.Messages;
    using ClusterKit.NodeManager.Client.ORM;
    using ClusterKit.NodeManager.Messages;
    using ClusterKit.Security.Attributes;

    using JetBrains.Annotations;

    /// <summary>
    /// The node manager api
    /// </summary>
    [ApiDescription("The main ClusterKit node managing methods", Name = "Root")]
    public class NodeManagerApi
    {
        /// <summary>
        /// The actor system.
        /// </summary>
        private readonly ActorSystem actorSystem;

        /// <summary>
        /// The address of the cluster nuget repository
        /// </summary>
        private readonly string feedUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeManagerApi"/> class.
        /// </summary>
        /// <param name="actorSystem">
        /// The actor system.
        /// </param>
        public NodeManagerApi(ActorSystem actorSystem)
        {
            this.actorSystem = actorSystem;
            this.feedUrl = actorSystem.Settings.Config.GetString(NodeManagerActor.PackageRepositoryUrlPath);
            this.AkkaTimeout = ConfigurationUtils.GetRestTimeout(actorSystem);
        }

        /// <summary>
        /// Gets the list of packages in the nuget repository
        /// </summary>
        [UsedImplicitly]
        [DeclareConnection("The packages in the Nuget repository")]
        [RequireSession]
        [RequireUser]
        [RequirePrivilege(Privileges.GetPackages, Scope = EnPrivilegeScope.User)]
        public NugetPackagesConnection NugetPackages => new NugetPackagesConnection(this.feedUrl);

        /// <summary>
        /// Gets timeout for actor system requests
        /// </summary>
        private TimeSpan AkkaTimeout { get; }

        /// <summary>
        /// Gets current cluster active nodes descriptions
        /// </summary>
        /// <returns>The list of descriptions</returns>
        [UsedImplicitly]
        [DeclareField("The list of known active nodes")]
        [RequireSession]
        [RequireUser]
        [RequirePrivilege(Privileges.GetActiveNodeDescriptions, Scope = EnPrivilegeScope.User)]
        public async Task<List<NodeDescription>> GetActiveNodeDescriptions()
        {
            var activeNodeDescriptions =
                await this.actorSystem.ActorSelection(GetManagerActorProxyPath())
                    .Ask<List<NodeDescription>>(new ActiveNodeDescriptionsRequest(), this.AkkaTimeout);

            return
                activeNodeDescriptions.OrderBy(n => n.NodeTemplate)
                    .ThenBy(n => n.ContainerType)
                    .ThenBy(n => n.NodeAddress.ToString())
                    .ToList();
        }

        /// <summary>
        /// Gets the list of available packages from local cluster repository
        /// </summary>
        /// <returns>The list of available packages</returns>
        [UsedImplicitly]
        [DeclareField("The list of available packages from local cluster repository",
            Converter = typeof(ArrayConverter<PackageFamily.Converter, PackageFamily>))]
        [RequireSession]
        [RequireUser]
        [RequirePrivilege(Privileges.GetPackages, Scope = EnPrivilegeScope.User)]
        public Task<List<Launcher.Messages.PackageDescription>> GetPackages()
        {
            return
                this.actorSystem.ActorSelection(GetManagerActorProxyPath())
                    .Ask<List<Launcher.Messages.PackageDescription>>(new PackageListRequest(), this.AkkaTimeout);
        }

        /// <summary>
        /// Gets current cluster node template usage for debug purposes
        /// </summary>
        /// <returns>Current cluster statistics</returns>
        [UsedImplicitly]
        [DeclareField("Current cluster node template usage for debug purposes")]
        [RequireSession]
        [RequireUser]
        [RequirePrivilege(Privileges.GetTemplateStatistics, Scope = EnPrivilegeScope.User)]
        public async Task<TemplatesUsageStatistics> GetTemplateStatistics()
        {
            return
                await this.actorSystem.ActorSelection(GetManagerActorProxyPath())
                    .Ask<TemplatesUsageStatistics>(new TemplatesStatisticsRequest(), this.AkkaTimeout);
        }

        /// <summary>
        /// The connection to the <see cref="NodeTemplate"/>
        /// </summary>
        /// <param name="context">The request context</param>
        /// <returns>The data connection</returns>
        [UsedImplicitly]
        [DeclareConnection(CanCreate = true, CreateDescription = "Creates the new node template", CanDelete = true,
            DeleteDescription = "Deletes the node template", CanUpdate = true,
            UpdateDescription = "Updates the node template", Description = "Node templates")]
        [RequireSession]
        [RequireUser]
        [RequirePrivilege(Privileges.NodeTemplate, Scope = EnPrivilegeScope.User,
            AddActionNameToRequiredPrivilege = true)]
        public Connection<NodeTemplate, int> NodeTemplates(RequestContext context)
        {
            return new Connection<NodeTemplate, int>(
                this.actorSystem,
                GetManagerActorProxyPath(),
                this.AkkaTimeout,
                context);
        }

        /// <summary>
        /// The connection to the <see cref="NugetFeed"/>
        /// </summary>
        /// <param name="context">The request context</param>
        /// <returns>The data connection</returns>
        [UsedImplicitly]
        [DeclareConnection(CanCreate = true, CreateDescription = "Creates the new nuget feed link", CanDelete = true,
            DeleteDescription = "Deletes the nuget feed link", CanUpdate = true,
            UpdateDescription = "Updates the nuget feed link", Description = "Node templates")]
        [RequirePrivilege(Privileges.NugetFeed, Scope = EnPrivilegeScope.User, AddActionNameToRequiredPrivilege = true)]
        public Connection<NugetFeed, int> NugetFeeds(RequestContext context)
        {
            return new Connection<NugetFeed, int>(
                this.actorSystem,
                GetManagerActorProxyPath(),
                this.AkkaTimeout,
                context);
        }

        /// <summary>
        /// The connection to the <see cref="Role"/>
        /// </summary>
        /// <param name="context">The request context</param>
        /// <returns>The data connection</returns>
        [UsedImplicitly]
        [DeclareConnection(CanCreate = true, CreateDescription = "Creates the new draft release", CanUpdate = true,
            UpdateDescription = "Updates the draft release", CanDelete = true,
            DeleteDescription = "Removes the draft release", Description = "ClusterKit managing system security roles")]
        [RequirePrivilege(Privileges.Release, Scope = EnPrivilegeScope.User, AddActionNameToRequiredPrivilege = true)]
        public ReleaseConnection Releases(RequestContext context)
        {
            return new ReleaseConnection(
                this.actorSystem,
                GetManagerActorProxyPath(),
                this.AkkaTimeout,
                context);
        }

        /// <summary>
        /// Request to server to reload package list
        /// </summary>
        /// <returns>Success of the operation</returns>
        [UsedImplicitly]
        [DeclareMutation(Description = "Request to server to reload package list")]
        [RequireSession]
        [RequireUser]
        [RequirePrivilege(Privileges.ReloadPackages, Scope = EnPrivilegeScope.User)]
        [LogAccess]
        public async Task<MutationResult<bool>> ReloadPackages()
        {
            var result =
                await this.actorSystem.ActorSelection(GetManagerActorProxyPath())
                    .Ask<bool>(new ReloadPackageListRequest(), this.AkkaTimeout);
            return new MutationResult<bool> { Result = result };
        }

        /// <summary>
        /// The connection to the <see cref="Role"/>
        /// </summary>
        /// <param name="context">The request context</param>
        /// <returns>The data connection</returns>
        [UsedImplicitly]
        [DeclareConnection(CanCreate = true, CreateDescription = "Creates the new managing system role",
            CanUpdate = true, UpdateDescription = "Updates the managing system role",
            Description = "ClusterKit managing system security roles")]
        [RequirePrivilege(Privileges.Role, Scope = EnPrivilegeScope.User, AddActionNameToRequiredPrivilege = true)]
        public RolesConnection Roles(RequestContext context)
        {
            return new RolesConnection(
                this.actorSystem,
                GetManagerActorProxyPath(),
                this.AkkaTimeout,
                context);
        }

        /// <summary>
        /// The connection to the <see cref="SeedAddress"/>
        /// </summary>
        /// <param name="context">The request context</param>
        /// <returns>The data connection</returns>
        [UsedImplicitly]
        [DeclareConnection(CanCreate = true, CreateDescription = "Creates the new seed address", CanDelete = true,
            DeleteDescription = "Deletes the seed address", CanUpdate = true,
            UpdateDescription = "Updates the seed address", Description = "Node templates")]
        [RequirePrivilege(Privileges.SeedAddress, Scope = EnPrivilegeScope.User, AddActionNameToRequiredPrivilege = true)]
        public Connection<SeedAddress, int> SeedAddresses(RequestContext context)
        {
            return new Connection<SeedAddress, int>(
                this.actorSystem,
                GetManagerActorProxyPath(),
                this.AkkaTimeout,
                context);
        }

        /// <summary>
        /// Manual node upgrade request
        /// </summary>
        /// <param name="address">Address of node to upgrade</param>
        /// <returns>Execution task</returns>
        [UsedImplicitly]
        [DeclareMutation("Manual node reboot request")]
        [RequireSession]
        [RequireUser]
        [RequirePrivilege(Privileges.UpgradeNode, Scope = EnPrivilegeScope.User)]
        [LogAccess]
        public async Task<MutationResult<bool>> UpgradeNode(string address)
        {
            var result =
                await this.actorSystem.ActorSelection(GetManagerActorProxyPath())
                    .Ask<bool>(new NodeUpgradeRequest { Address = Address.Parse(address) }, this.AkkaTimeout);
            return new MutationResult<bool> { Result = result };
        }

        /// <summary>
        /// The connection to the <see cref="User"/>
        /// </summary>
        /// <param name="context">The request context</param>
        /// <returns>The data connection</returns>
        [UsedImplicitly]
        [DeclareConnection(CanCreate = true, CreateDescription = "Creates the new user", CanUpdate = true,
            UpdateDescription = "Updates the user", Description = "ClusterKit managing system users")]
        [RequirePrivilege(Privileges.User, Scope = EnPrivilegeScope.User, AddActionNameToRequiredPrivilege = true)]
        public UsersConnection Users(RequestContext context)
        {
            return new UsersConnection(
                this.actorSystem,
                GetManagerActorProxyPath(),
                this.AkkaTimeout,
                context);
        }

        /// <summary>
        /// Gets akka actor path for database worker
        /// </summary>
        /// <returns>Akka actor path</returns>
        internal static string GetManagerActorProxyPath() => "/user/NodeManager/NodeManagerProxy";
    }
}