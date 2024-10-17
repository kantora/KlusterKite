// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MonitoringApi.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Monitoring api
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Monitoring
{
    using System;
    using System.Threading.Tasks;

    using Akka.Actor;
    using Akka.Event;
    using JetBrains.Annotations;

    using KlusterKite.API.Attributes;
    using KlusterKite.API.Attributes.Authorization;
    using KlusterKite.LargeObjects.Client;
    using KlusterKite.Monitoring.Client;
    using KlusterKite.Monitoring.Messages;

    /// <summary>
    /// Monitoring api
    /// </summary>
    [ApiDescription(Description = "KlusterKite monitoring api", Name = "Root")]
    public class MonitoringApi
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
        /// Initializes a new instance of the <see cref="MonitoringApi"/> class.
        /// </summary>
        /// <param name="system">
        /// The actor system.
        /// </param>
        public MonitoringApi(ActorSystem system)
        {
            this.system = system;
            this.systemTimeout = system.Settings.Config.GetTimeSpan("KlusterKite.AskTimeout", TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Gets the last cluster scan result.
        /// Changes as system receives the scan results from nodes
        /// </summary>
        /// <returns>The cluster scan result</returns>
        [UsedImplicitly]
        [RequireSession]
        [RequireUser]
        [RequirePrivilege(Privileges.GetClusterTree, Scope = EnPrivilegeScope.User)]
        [DeclareField("Gets the last cluster scan result. Changes as system receives the scan results from nodes")]
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
        [UsedImplicitly]
        [RequireSession]
        [RequireUser]
        [RequirePrivilege(Privileges.InitiateScan, Scope = EnPrivilegeScope.User)]
        [DeclareMutation("Initiates the new actor system scan")]
        public bool InitiateScan()
        {
            this.system.ActorSelection("/user/Monitoring/ClusterScannerProxy").Tell(new ClusterScanRequest());
            return true;
        }
    }
}