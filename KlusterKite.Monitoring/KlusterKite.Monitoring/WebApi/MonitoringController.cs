// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MonitoringController.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Manages web requests to describe current cluster health state
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Monitoring.WebApi
{
    using System;
    using System.Threading.Tasks;

    using Akka.Actor;
    using Akka.Event;
    using KlusterKite.LargeObjects.Client;
    using KlusterKite.Monitoring.Client;
    using KlusterKite.Monitoring.Messages;
    using KlusterKite.Web.Authorization.Attributes;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Manages web requests to describe current cluster health state
    /// </summary>
    [Route("api/1.x/klusterkite/monitoring")]
    [RequireUser]
    public class MonitoringController : Controller
    {
        /// <summary>
        /// The actor system.
        /// </summary>
        private readonly ActorSystem system;

        /// <summary>
        /// The actor system negotiation timeout
        /// </summary>
        private readonly TimeSpan systemTimeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitoringController"/> class.
        /// </summary>
        /// <param name="system">
        /// The actor system.
        /// </param>
        public MonitoringController(ActorSystem system)
        {
            this.system = system;
            this.systemTimeout = system.Settings.Config.GetTimeSpan("KlusterKite.AskTimeout", TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Gets the last cluster scan result.
        /// Changes as system receives the scan results from nodes
        /// </summary>
        /// <returns>The cluster scan result</returns>
        [Route("getScanResult")]
        [RequireUserPrivilege(Privileges.GetClusterTree)]
        public async Task<ClusterTree> GetClusterTree()
        {
            try
            {
                var notification = await this.system
                            .ActorSelection("/user/Monitoring/ClusterScannerProxy")
                            .Ask<ParcelNotification>(new ClusterScanResultRequest(), this.systemTimeout);

                return await notification.Receive(this.system) as ClusterTree;
            }
            catch (Exception exception)
            {
                this.system.Log.Log(LogLevel.ErrorLevel, exception, "{Type}: error on GetClusterTree", this.GetType().Name);
                throw;
            }
        }

        /// <summary>
        /// Initiates the new actor system scan
        /// </summary>
        /// <returns>The success of the operation</returns>
        [Route("initiateScan")]
        [RequireUserPrivilege(Privileges.InitiateScan)]
        public bool InitiateScan()
        {
            this.system.ActorSelection("/user/Monitoring/ClusterScannerProxy").Tell(new ClusterScanRequest());
            return true;
        }
    }
}