﻿using System.Threading.Tasks;

namespace ClusterKit.Monitoring.Actors
{
    using System;

    using Akka.Actor;
    using Akka.Event;

    using ClusterKit.Core.Ping;
    using ClusterKit.Core.Utils;
    using ClusterKit.Monitoring.Messages;

    /// <summary>
    /// Pings cluster nodes
    /// </summary>
    public class PingerActor : ReceiveActor
    {
        /// <summary>
        /// The ping frequency
        /// </summary>
        private readonly TimeSpan pingFrequency;

        /// <summary>
        /// The ping timeout
        /// </summary>
        private readonly TimeSpan pingTimeOut;

        /// <summary>
        /// Node adress to check
        /// </summary>
        private Address nodeAddress;

        /// <summary>
        /// Node adress to check
        /// </summary>
        private ICanTell nodeToPing;

        public PingerActor()
        {
            this.pingTimeOut = Context.System.Settings.Config.GetTimeSpan(
                "ClusterKit.Monitoring.PingTimeout",
                TimeSpan.FromSeconds(2),
                false);

            this.pingFrequency = Context.System.Settings.Config.GetTimeSpan(
                "ClusterKit.Monitoring.PingFrequency",
                TimeSpan.FromSeconds(2),
                false);

            this.Receive<Address>(a => this.Initialize(a));
        }

        /// <summary>
        /// User overridable callback: '''By default it disposes of all children and then calls `postStop()`.'''
        ///                 <p/>
        ///                 Is called on a crashed Actor right BEFORE it is restarted to allow clean
        ///                 up of resources before Actor is terminated.
        /// </summary>
        /// <param name="reason">the Exception that caused the restart to happen.</param><param name="message">optionally the current message the actor processed when failing, if applicable.</param>
        protected override void PreRestart(Exception reason, object message)
        {
            if (this.nodeAddress != null)
            {
                this.Self.Tell(this.nodeAddress);
            }

            base.PreRestart(reason, message);
        }

        /// <summary>
        /// Initialization of actor normal work
        /// </summary>
        /// <param name="address">Node address to ping</param>
        /// <returns>Async task</returns>
        private Task Initialize(Address address)
        {
            this.nodeAddress = address;
            this.nodeToPing = Context.System.ActorSelection($"{address.ToString()}/user/Core/Ping");

            this.Become(
                () =>
                    {
                        this.Receive<TimeToPing>(m => this.Ping());
                    });

            Context.System.Scheduler.ScheduleTellRepeatedly(
                TimeSpan.Zero,
                this.pingFrequency,
                this.Self,
                new TimeToPing(),
                this.Self
                );

            return Task.CompletedTask;
        }

        /// <summary>
        /// Measures ping time to selected node
        /// </summary>
        /// <returns>Async task</returns>
        private async Task Ping()
        {
            var now = DateTimeWrapper.Now;
            try
            {
                await
                    this.nodeToPing.Ask<PongMessage>(
                        new PingMessage(),
                        this.pingTimeOut);
            }
            catch (Exception)
            {
                Context.Parent.Tell(new PingMeasurement { Address = this.nodeAddress });
                return;
            }

            Context.Parent.Tell(new PingMeasurement
            {
                Address = this.nodeAddress,
                Result = DateTimeWrapper.Now - now
            });
        }

        /// <summary>
        /// Private notification to perfom measurement
        /// </summary>
        private class TimeToPing
        {
        }
    }
}