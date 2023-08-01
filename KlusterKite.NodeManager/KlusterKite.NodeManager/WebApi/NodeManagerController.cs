// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeManagerController.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Serves node management api functions
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.WebApi
{
    using System;
    using System.Threading.Tasks;

    using Akka.Actor;
    using Akka.Event;

    using JetBrains.Annotations;

    using KlusterKite.Core;
    using KlusterKite.NodeManager.Client;
    using KlusterKite.NodeManager.Launcher.Messages;
    using KlusterKite.NodeManager.Messages;
    using KlusterKite.Web.Authorization.Attributes;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Serves node management api functions
    /// </summary>
    [UsedImplicitly]
    [RequireSession]
    [Route("api/1.x/klusterkite/nodemanager")]
    public class NodeManagerController : Controller
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NodeManagerController"/> class.
        /// </summary>
        /// <param name="system">
        /// The system.
        /// </param>
        public NodeManagerController(ActorSystem system)
        { 
            this.System = system;
            this.AkkaTimeout = ConfigurationUtils.GetRestTimeout(system);
        }

        /// <summary>
        /// Gets timeout for actor system requests
        /// </summary>
        private TimeSpan AkkaTimeout { get; }

        /// <summary>
        /// Gets the actor system
        /// </summary>
        private ActorSystem System { get; }

        /// <summary>
        /// Gets configuration for new empty node
        /// </summary>
        /// <param name="request">The configuration request</param>
        /// <returns>The configuration to apply</returns>
        [Route("getConfiguration")]
        [HttpPost]
        [RequireClientPrivilege(Privileges.GetConfiguration)]
        public async Task<IActionResult> GetConfiguration([FromBody] NewNodeTemplateRequest request)
        {
            object result;
            try
            {
                result = await this.System.ActorSelection(this.GetManagerActorProxyPath())
                             .Ask<object>(request, this.AkkaTimeout);
            }
            catch (Exception exception)
            {
                this.System.Log.Error(exception, "{Type}: GetConfiguration exception", this.GetType().Name);
                throw;
            }

            var configuration = result as NodeStartUpConfiguration;
            if (configuration != null)
            {
                this.System.Log.Info("{Type}: sending configuration", this.GetType().Name);
                return this.Ok(configuration);
            }

            var waitMessage = result as NodeStartupWaitMessage;
            if (waitMessage != null)
            {
                this.System.Log.Info(
                    "{Type}: cluster is full. Container {ContainerType} with framework {FrameworkName} in not needed now",
                    this.GetType().Name,
                    request.ContainerType,
                    request.FrameworkRuntimeType);
                this.HttpContext.Response.Headers.Add("Retry-After", ((int)waitMessage.WaitTime.TotalSeconds).ToString());
                return new StatusCodeResult(503);
            }

            this.System.Log.Info("{Type}: NewNodeTemplateRequest - unknown response of type {ResponseType}", this.GetType().Name, result?.GetType().Name);
            return new StatusCodeResult(500);
        }

        /// <summary>
        /// Manual node upgrade request
        /// </summary>
        /// <param name="address">Address of node to upgrade</param>
        /// <returns>Execution task</returns>
        [Route("upgradeNode")]
        [HttpPost]
        [RequireUserPrivilege(Privileges.UpgradeNode)]
        public async Task<IActionResult> UpgradeNode(Address address)
        {
            var result = await this.System.ActorSelection(this.GetManagerActorProxyPath()).Ask<bool>(new NodeUpgradeRequest { Address = address }, this.AkkaTimeout);
            if (!result)
            {
                return this.NotFound();
            }

            return this.Ok();
        }

        /// <summary>
        /// Gets akka actor path for database worker
        /// </summary>
        /// <returns>Akka actor path</returns>
        private string GetManagerActorProxyPath() => "/user/NodeManager/NodeManagerProxy";
    }
}