// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ParcelNotification.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Notification about awaiting large object parcel
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.LargeObjects.Client
{
    using System;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using Akka.Event;
    using Akka.Actor;
    using KlusterKite.Core.Utils;

    using JetBrains.Annotations;

    /// <summary>
    /// Notification about awaiting large object parcel
    /// </summary>
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class ParcelNotification
    {
        /// <summary>
        /// Gets or sets the host name of parcel server
        /// </summary>
        [UsedImplicitly]
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the port number of parcel server
        /// </summary>
        [UsedImplicitly]
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the parcel uid
        /// </summary>
        [UsedImplicitly]
        public Guid Uid { get; set; }

        /// <summary>
        /// Gets or sets the parcel payload type name
        /// </summary>
        public string PayloadTypeName { get; set; }

        /// <summary>
        /// Gets or sets the current receive attempt number
        /// </summary>
        public int ReceiveAttempt { get; set; }

        /// <summary>
        /// Gets the type from type name
        /// </summary>
        /// <returns>The type or null, in case this type could not be locally found</returns>
        public Type GetPayloadType()
        {
            return Type.GetType(this.PayloadTypeName);
        }

        /// <summary>
        /// Receives payload from parcel server
        /// </summary>
        /// <param name="system">The actor system</param>
        /// <returns>The payload</returns>
        /// <exception cref="TypeLoadException">The type in parcel is unknown to local system</exception>
        /// <exception cref="TimeoutException">The timeout occured while reading from stream</exception>
        /// <exception cref="ParcelNotFoundException">The parcel was already removed from the server</exception>
        /// <exception cref="ParcelServerErrorException">The server could not be contacted or some other error</exception>
        /// <exception cref="ParcelServerUnknownStatus">The server returned unknown status</exception>
        public virtual async Task<object> Receive(ActorSystem system)
        {
            this.ReceiveAttempt++;
            var timeOut = system.Settings.Config.GetTimeSpan("KlusterKite.LargeObjects.TcpReadTimeout", TimeSpan.FromSeconds(10));

            if (system.Settings.Config.GetBoolean("KlusterKite.LargeObjects.LogReceive"))
            {
                system.Log.Info("{Type}: Receiving {PayloadType} payload", this.GetType().Name, this.PayloadTypeName);
            }

            var payloadType = this.GetPayloadType();
            if (payloadType == null)
            {
                throw new ParcelTypeLoadException { Notification = this };
            }

            using (var client = new TcpClient())
            {
                try
                {
                    await client.ConnectAsync(this.Host, this.Port);
                }
                catch (Exception)
                {
                    throw new ParcelServerErrorException("The server could not be connected") { Notification = this };
                }

                using (var stream = client.GetStream())
                {
                    await stream.WriteAsync(this.Uid.ToByteArray(), 0, 16);
                    byte[] statusBuffer = new byte[1];
                    var readOperation = await this.ReadWithTimeout(stream, statusBuffer, 0, 1, timeOut);
                    if (readOperation != 1)
                    {
                        throw new ParcelServerErrorException("Null response from the server on status read") { Notification = this };
                    }

                    var status = (EnParcelServerResponseCode)statusBuffer[0];

                    switch (status)
                    {
                        case EnParcelServerResponseCode.Ok:
                            break;
                        case EnParcelServerResponseCode.BadRequest:
                            throw new ParcelServerErrorException("Bad request") { Notification = this };
                        case EnParcelServerResponseCode.NotFound:
                            throw new ParcelNotFoundException { Notification = this };
                        default:
                            throw new ParcelServerUnknownStatus { Notification = this };
                    }

                    byte[] lengthBuffer = new byte[4];
                    readOperation = await this.ReadWithTimeout(stream, lengthBuffer, 0, 4, timeOut);
                    
                    if (readOperation != 4)
                    {
                        throw new ParcelServerErrorException("Null response from the server on length read");
                    }

                    var length = BitConverter.ToInt32(lengthBuffer, 0);
                    var buffer = new byte[length];
                    const int ChunkSize = 1024;
                    var bytesRead = 0;
                    int chunkRead;
                    while ((chunkRead = await this.ReadWithTimeout(stream, buffer, bytesRead, length - bytesRead > ChunkSize ? ChunkSize : length - bytesRead, timeOut)) > 0)
                    {
                        bytesRead += chunkRead;
                    }

                    if (bytesRead != length)
                    {
                        throw new ParcelServerErrorException("Unexpected end of data") { Notification = this };
                    }

                    readOperation = await this.ReadWithTimeout(stream, buffer, 0, length, timeOut);
                    if (readOperation == -1)
                    {
                        throw new ParcelServerErrorException("Null response from the server on data read") { Notification = this };
                    }

                    return system.Serialization.FindSerializerForType(payloadType).FromBinary(buffer, payloadType);
                }
            }
        }

        /// <summary>
        /// Receive parcel's payload and sends it contents to the specified recipient
        /// </summary>
        /// <param name="system">The actor system</param>
        /// <param name="recipient">The recipient of the payload and/or exception </param>
        /// <param name="sender">The sender</param>
        /// <returns>The executing task</returns>
        public virtual Task ReceiveWithPipeTo(ActorSystem system, ICanTell recipient, IActorRef sender)
        {
            return this.Receive(system).PipeTo(
                recipient, 
                sender, 
                failure: e => this.HandlePipedException(system, recipient, sender, e));
        }

        /// <summary>
        /// Handles the PipeTo exception
        /// </summary>
        /// <param name="system">
        /// The actor system.
        /// </param>
        /// <param name="recipient">The recipient of the payload and/or exception </param>
        /// <param name="sender">The sender</param>
        /// <param name="exception">
        /// The exception
        /// </param>
        /// <returns>
        /// Routed object
        /// </returns>
        private object HandlePipedException(ActorSystem system, ICanTell recipient, IActorRef sender, Exception exception)
        {
            exception = this.ExtractException(exception);
            exception.Match()
                .With<ParcelTimeoutException>(
                () =>
                    {
                        var maxNumberOfAttempts = system.Settings.Config.GetInt("KlusterKite.LargeObjects.MaxReadAttempts", 5);
                        if (this.ReceiveAttempt < maxNumberOfAttempts)
                        {
                            var rereadInterval = system.Settings.Config.GetTimeSpan("KlusterKite.LargeObjects.RereadInterval", TimeSpan.FromSeconds(5));
                            system.Scheduler.ScheduleTellOnce(rereadInterval, recipient, this, sender);
                        }
                        else
                        {
                            exception = new ParcelServerErrorException("Server is unresponsive");
                        }
                    });

            return exception;
        }

        /// <summary>
        /// Extracts real exception from <see cref="AggregateException"/>
        /// </summary>
        /// <param name="e">The exception</param>
        /// <returns>The real exception</returns>
        private Exception ExtractException(Exception e)
        {
            var aggregate = e as AggregateException;
            if (aggregate != null && aggregate.InnerExceptions.Count > 0)
            {
                return this.ExtractException(aggregate.InnerExceptions.First());
            }

            return e;
        }

        /// <summary>
        /// Reads from the stream with provided timeout
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <param name="buffer">The byte buffer</param>
        /// <param name="offset">The stream offset</param>
        /// <param name="length">The number of bytes to read</param>
        /// <param name="timeout">The timeout</param>
        /// <returns>The number of read bytes</returns>
        private async Task<int> ReadWithTimeout(
            NetworkStream stream,
            byte[] buffer,
            int offset,
            int length,
            TimeSpan timeout)
        {
            var tokenSource = new CancellationTokenSource();
            var ct = tokenSource.Token;

            var task = stream.ReadAsync(buffer, offset, length, ct); 
            if (await Task.WhenAny(task, Task.Delay(timeout, ct)) == task)
            {
                tokenSource.Cancel();
                return task.Result;
            }

            tokenSource.Cancel();
            throw new ParcelTimeoutException { Notification = this };
        }
    }
}
