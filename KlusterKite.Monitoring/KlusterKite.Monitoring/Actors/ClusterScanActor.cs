// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ClusterScanActor.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Actor, that scans whole cluster and gets actor's tree
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Monitoring.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Akka.Actor;
    using Akka.Cluster;
    using Akka.Event;
    using JetBrains.Annotations;

    using KlusterKite.LargeObjects;
    using KlusterKite.LargeObjects.Client;
    using KlusterKite.Monitoring.Client.Messages;
    using KlusterKite.Monitoring.Messages;

    /// <summary>
    /// Actor, that scans whole cluster and gets actor's tree
    /// </summary>
    [UsedImplicitly]
    public class ClusterScanActor : ReceiveActor
    {
        /// <summary>
        /// Last saved cluster tree result
        /// </summary>
        private readonly ClusterTree clusterTree = new ClusterTree();

        /// <summary>
        /// The list of the current cluster nodes
        /// </summary>
        private readonly Dictionary<Address, Member> nodes = new Dictionary<Address, Member>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ClusterScanActor"/> class.
        /// </summary>
        public ClusterScanActor()
        {
            Cluster.Get(Context.System)
               .Subscribe(
                   this.Self,
                   ClusterEvent.InitialStateAsEvents,
                   typeof(ClusterEvent.MemberRemoved),
                   typeof(ClusterEvent.MemberUp));

            this.Receive<ClusterEvent.MemberUp>(m => this.OnNodeUp(m.Member));
            this.Receive<ClusterEvent.MemberRemoved>(m => this.OnNodeDown(m.Member));

            this.Receive<ClusterScanRequest>(m => this.OnClusterScanRequest());
            this.Receive<ClusterScanResultRequest>(
                m => Context.GetParcelManager()
                        .Tell(
                                new Parcel
                                {
                                    Payload = this.clusterTree,
                                    Recipient = this.Sender
                                },
                                this.Self));
            this.ReceiveAsync<ParcelNotification>(this.OnParcel);
            Context.System.Log.Log(LogLevel.InfoLevel, "{Type}: started", [this.GetType().Name]);
        }

        /// <summary>
        /// Handles the <see cref="ClusterEvent.MemberRemoved"/> event
        /// </summary>
        /// <param name="member">The cluster member</param>
        private void OnNodeDown(Member member)
        {
            this.nodes.Remove(member.Address);
        }

        /// <summary>
        /// Handles the <see cref="ClusterEvent.MemberUp"/> event
        /// </summary>
        /// <param name="member">The cluster member</param>
        private void OnNodeUp(Member member)
        {
            this.nodes[member.Address] = member;
        }

        /// <summary>
        /// Constructs the node name
        /// </summary>
        /// <param name="n">The node</param>
        /// <returns>The node name</returns>
        private string GetNodeName(Node n)
        {
            return string.IsNullOrWhiteSpace(n.Name) ? this.Sender.Path.Address.ToString() : n.Name;
        }

        /// <summary>
        /// Performs the global cluster scan
        /// </summary>
        private void OnClusterScanRequest()
        {
            Context.System.Log.Info("{Type}: initiating cluster scan", this.GetType().Name);

            foreach (var address in this.nodes.Keys)
            {
                Context.ActorSelection($"{address}/user/Monitoring/Scanner").Tell(new ActorSystemScanRequest());
            }

            this.clusterTree.Nodes.Clear();
            Context.System.Log.Info("{Type}: cluster scan was initiated", this.GetType().Name);
        }

        /// <summary>
        /// Handles the scan result from remote node.
        /// As scan result can be very large, it is received via parcel.
        /// </summary>
        /// <param name="parcelNotification">The parcel notification</param>
        /// <returns>The async task</returns>
        private async Task OnParcel(ParcelNotification parcelNotification)
        {
            if (parcelNotification.GetPayloadType() != typeof(Node))
            {
                Context.System.Log.Info(
                    "{Type}: received parcel with unexpected content of {ParcelContentType}",
                    this.GetType().Name,
                    parcelNotification.PayloadTypeName);
                return;
            }

            try
            {
                Context.System.Log.Info("{Type}: received parcel from {Sender}", this.GetType().Name, this.Sender.ToString());
                var node = await parcelNotification.Receive(Context.System) as Node;
                this.clusterTree.Nodes[this.GetNodeName(node)] = node;
            }
            catch (Exception exception)
            {
                Context.System.Log.Error(exception, "{Type}: error while receiving parcel", this.GetType().Name);
            }
        }
    }
}