﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WatcherActor.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   Watcher actor. It's main purpose to monitor any cluster changes and store complete
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.Monitoring.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Akka.Actor;
    using Akka.Cluster;
    using Akka.DI.Core;
    using Akka.Event;

    using ClusterKit.Monitoring.Messages;

    using Microsoft.AspNet.SignalR;

    /// <summary>
    /// Watcher actor. It's main purpose to monitor any cluster changes and store complete data about current cluster health
    /// </summary>
    public class WatcherActor : ReceiveActor
    {
        /// <summary>
        /// Current cluster members
        /// </summary>
        private readonly Dictionary<Address, MemberDescription> clusterMembers = new Dictionary<Address, MemberDescription>();

        /// <summary>
        /// The current role leader.
        /// </summary>
        private readonly Dictionary<string, Address> currentRoleLeader = new Dictionary<string, Address>();

        /// <summary>
        /// Current list of node ping testers
        /// </summary>
        private readonly Dictionary<Address, IActorRef> pingers = new Dictionary<Address, IActorRef>();

        /// <summary>
        /// The current cluster leader.
        /// </summary>
        private Address currentClusterLeader;

        /// <summary>
        /// Timeout, after wich member removed from cluster will be removed from monitoring
        /// </summary>
        private TimeSpan removeMemberTimeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="WatcherActor"/> class.
        /// </summary>
        public WatcherActor()
        {
            this.Receive<ClusterEvent.MemberStatusChange>(m => this.MemberStatusChange(m.Member));
            this.Receive<ClusterEvent.ReachabilityEvent>(
                m => this.ReachabilityChanged(m.Member, m is ClusterEvent.ReachableMember));
            this.Receive<ClusterEvent.LeaderChanged>(m => this.ClusterLeaderChanged(m.Leader));
            this.Receive<ClusterEvent.RoleLeaderChanged>(m => this.RoleLeaderChanged(m.Role, m.Leader));
            this.Receive<ClusterMemberListRequest>(m => this.OnClusterMemberListRequest());

            this.Receive<PingMeasurement>(m => this.OnPingMeasurement(m));
            this.Receive<TimeToBroadcastMemebers>(m => this.BroadcastMembers());
            this.Receive<CheckRemovedMember>(m => this.OnCheckRemovedMember(m));

            Cluster.Get(Context.System)
                .Subscribe(
                    this.Self,
                    ClusterEvent.InitialStateAsEvents,
                    new[] { typeof(ClusterEvent.IClusterDomainEvent) });

            this.removeMemberTimeout = Context.System.Settings.Config.GetTimeSpan(
                "ClusterKit.Monitoring.RemoveMemberTimeout",
                TimeSpan.FromHours(1),
                true);

            var broadCastFrequency =
                Context.System.Settings.Config.GetTimeSpan(
                    "ClusterKit.Monitoring.BroadcastClientFrequency",
                    TimeSpan.FromSeconds(5),
                    false);

            Context.System.Scheduler.ScheduleTellRepeatedly(
                 broadCastFrequency,
                 broadCastFrequency,
                 this.Self,
                 new TimeToBroadcastMemebers(),
                 this.Self);
        }

        protected override void PostRestart(Exception reason)
        {
            if (reason != null)
            {
                Context.GetLogger().Error(reason, "{Type}: Exception in actor", this.GetType().Name, reason.Message);
            }
            else
            {
                Context.GetLogger().Error("{Type}: Actor restarted with no reason", this.GetType().Name);
            }
            base.PostRestart(reason);
        }

        /// <summary>
        /// Sends new node description to online clients
        /// </summary>
        /// <param name="description">New node description</param>
        private static void SendNewDescription(MemberDescription description)
        {
            var context = GlobalHost.ConnectionManager.GetHubContext<MonitoringHub>();
            context.Clients.All.memberUpdate(description);
        }

        /// <summary>
        /// Sends actual info to all clients
        /// </summary>
        /// <returns>Async task</returns>
        private Task BroadcastMembers()
        {
            var context = GlobalHost.ConnectionManager.GetHubContext<MonitoringHub>();
            context.Clients.All.reloadData(this.clusterMembers.Values.ToList());
            return Task.CompletedTask;
        }

        /// <summary>
        /// Processes cluster leader change
        /// </summary>
        /// <param name="leader">New leader address</param>
        /// <returns>Processing task</returns>
        private Task ClusterLeaderChanged(Address leader)
        {
            this.currentClusterLeader = leader;
            var currentLeader = this.clusterMembers.Values.FirstOrDefault(m => m.IsGlobalLeader);
            if (currentLeader != null)
            {
                currentLeader.IsGlobalLeader = false;
                SendNewDescription(currentLeader);
            }

            if (leader != null && this.clusterMembers.TryGetValue(leader, out currentLeader))
            {
                currentLeader.IsGlobalLeader = true;
                SendNewDescription(currentLeader);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Monitors current cluster members changing
        /// </summary>
        /// <param name="member">
        /// The member Status Change.
        /// </param>
        /// <returns>
        /// Processing task
        /// </returns>
        private Task MemberStatusChange(Member member)
        {
            MemberDescription description;
            if (!this.clusterMembers.TryGetValue(member.Address, out description))
            {
                description = new MemberDescription(member);
                this.clusterMembers[member.Address] = description;

                if (this.currentClusterLeader == member.Address)
                {
                    description.IsGlobalLeader = true;
                }

                description.RoleLeader.AddRange(this.currentRoleLeader.Where(p => p.Value == member.Address).Select(p => p.Key));
            }

            if (member.Status == MemberStatus.Up && !this.pingers.ContainsKey(member.Address))
            {
                var pinger = Context.ActorOf(Context.System.DI().Props<PingerActor>());
                pinger.Tell(member.Address);
                this.pingers[member.Address] = pinger;
            }

            if (member.Status == MemberStatus.Removed)
            {
                IActorRef pinger;
                if (this.pingers.TryGetValue(member.Address, out pinger))
                {
                    this.pingers.Remove(member.Address);
                    pinger.Tell(PoisonPill.Instance);
                }

                Context.System.Scheduler.ScheduleTellOnce(
                    this.removeMemberTimeout,
                    this.Self,
                    new CheckRemovedMember
                    {
                        Address = member.Address,
                        Uid = member.UniqueAddress.Uid
                    },
                    this.Self);
            }

            description.Roles = member.Roles.ToList();
            description.Status = member.Status;
            SendNewDescription(description);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks exited member for actual death. If they are still not online - removing from monitoring
        /// </summary>
        /// <param name="message">The removed member message</param>
        /// <returns>Async task</returns>
        private Task OnCheckRemovedMember(CheckRemovedMember message)
        {
            MemberDescription description;
            if (this.clusterMembers.TryGetValue(message.Address, out description)
                && description.Uid == message.Uid
                && description.Status == MemberStatus.Removed)
            {
                this.clusterMembers.Remove(message.Address);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Serves cluster member list request
        /// </summary>
        /// <returns>Current cluster member list</returns>
        private Task OnClusterMemberListRequest()
        {
            this.Sender.Tell(this.clusterMembers.Values.ToList());
            return Task.CompletedTask;
        }

        /// <summary>
        /// Writes actual ping measurement
        /// </summary>
        /// <param name="pingMeasurement">Perfomed measurement</param>
        /// <returns>Async task</returns>
        private Task OnPingMeasurement(PingMeasurement pingMeasurement)
        {
            MemberDescription description;
            if (this.clusterMembers.TryGetValue(pingMeasurement.Address, out description))
            {
                description.PingValue = pingMeasurement.Result?.TotalMilliseconds;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Processes node reachability event
        /// </summary>
        /// <param name="member">The changed node</param>
        /// <param name="isReachable">The value indicating whether node is reachable</param>
        /// <returns>Processing task</returns>
        private Task ReachabilityChanged(Member member, bool isReachable)
        {
            MemberDescription description;
            if (!this.clusterMembers.TryGetValue(member.Address, out description))
            {
                description = new MemberDescription(member);
                this.clusterMembers[member.Address] = description;
            }

            description.IsReachable = isReachable;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Processes role leader change
        /// </summary>
        /// <param name="role">The role</param>
        /// <param name="leader">New leader address</param>
        /// <returns>Processing task</returns>
        private Task RoleLeaderChanged(string role, Address leader)
        {
            this.currentRoleLeader[role] = leader;
            var currentLeader = this.clusterMembers.Values.FirstOrDefault(m => m.RoleLeader.Contains(role));
            if (currentLeader != null)
            {
                currentLeader.RoleLeader.Remove(role);
                SendNewDescription(currentLeader);
            }

            if (leader != null && this.clusterMembers.TryGetValue(leader, out currentLeader))
            {
                currentLeader.Roles.Add(role);
                SendNewDescription(currentLeader);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Private auto message to check unreachable members from monitoring
        /// </summary>
        private class CheckRemovedMember
        {
            /// <summary>
            /// Gets or sets the member address
            /// </summary>
            public Address Address { get; set; }

            /// <summary>
            /// Gets or sets the member address uid
            /// </summary>
            public int Uid { get; set; }
        }

        private class TimeToBroadcastMemebers
        {
        }
    }
}