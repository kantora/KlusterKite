// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RedisSessionTokenManager.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Redis based token manager
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Security.SessionRedis
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Akka.Actor;
    using Akka.Event;

    using JetBrains.Annotations;

    using KlusterKite.Core.Utils;
    using KlusterKite.Security.Attributes;

    using StackExchange.Redis;

    /// <summary>
    /// Redis based token manager
    /// </summary>
    /// <remarks>
    /// Async/await was removed as it caused performance issues.
    /// </remarks>
    [UsedImplicitly]
    public class RedisSessionTokenManager : ITokenManager
    {
        /// <summary>
        /// The redis connection string
        /// </summary>
        private readonly ConfigurationOptions redisConnectionString;

        /// <summary>
        /// The redis database number
        /// </summary>
        private readonly int redisDb;

        /// <summary>
        /// The actor system
        /// </summary>
        private readonly ActorSystem system;

        /// <summary>
        /// The token key prefix to store in redis
        /// </summary>
        private readonly string tokenKeyPrefix;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisSessionTokenManager"/> class.
        /// </summary>
        /// <param name="system">
        /// The system.
        /// </param>
        public RedisSessionTokenManager(ActorSystem system)
        {
            this.system = system;
            var connectionString = system.Settings.Config.GetString("KlusterKite.Security.SessionRedis.RedisConnection");

            var config = ConfigurationOptions.Parse(connectionString);
            var addressEndpoint = (DnsEndPoint)config.EndPoints.First();
            var port = addressEndpoint.Port;

            bool isIp = IsIpAddress(addressEndpoint.Host);
            if (!isIp)
            {
                var ip = Dns.GetHostEntryAsync(addressEndpoint.Host).GetAwaiter().GetResult();
                config.EndPoints.Remove(addressEndpoint);
                config.EndPoints.Add(ip.AddressList.First(), port);
            }

            this.redisConnectionString = config;

            this.redisDb = system.Settings.Config.GetInt("KlusterKite.Security.SessionRedis.RedisDb");
            this.tokenKeyPrefix = system.Settings.Config.GetString("KlusterKite.Security.SessionRedis.TokenKeyPrefix");
        }

        /// <inheritdoc />
        public Task<string> CreateAccessToken(AccessTicket session)
        {
            var tokenUid = Guid.NewGuid();
            var token = tokenUid.ToString("N");

            var data = session.SerializeToAkka(this.system);
            using (var connection = ConnectionMultiplexer.Connect(this.redisConnectionString))
            {
                var db = connection.GetDatabase(this.redisDb);

                var result = db.StringSet(
                    this.GetRedisAccessKey(token),
                    data,
                    session.Expiring.HasValue ? (TimeSpan?)(session.Expiring.Value - DateTimeOffset.Now) : null);

                if (result)
                {
                    return Task.FromResult(token);
                }

                var exception = new Exception("Session data server is unavailable");
                this.system.Log.Error(exception, "{Type}: Session data server is unavailable", this.GetType().Name);
                throw exception;
            }
        }

        /// <inheritdoc />
        public Task<string> CreateRefreshToken(RefreshTicket ticket)
        {
            var watch = new Stopwatch();
            watch.Start();

            var tokenUid = Guid.NewGuid();
            var token = tokenUid.ToString("N");

            var data = ticket.SerializeToAkka(this.system);
            using (var connection = ConnectionMultiplexer.Connect(this.redisConnectionString))
            {
                var db = connection.GetDatabase(this.redisDb);
                var result = db.StringSet(
                    this.GetRedisRefreshKey(token),
                    data,
                    ticket.Expiring.HasValue ? (TimeSpan?)(ticket.Expiring.Value - DateTimeOffset.Now) : null);
                if (result)
                {
                    return Task.FromResult(token);
                }

                var exception = new Exception("Session data server is unavailable");
                this.system.Log.Error(exception, "{Type}: Session data server is unavailable", this.GetType().Name);
                throw exception;
            }
        }

        /// <inheritdoc />
        public Task<bool> RevokeAccessToken(string token)
        {
            using (var connection = ConnectionMultiplexer.Connect(this.redisConnectionString))
            {
                var db = connection.GetDatabase(this.redisDb);
                return Task.FromResult(db.KeyDelete(this.GetRedisAccessKey(token)));
            }
        }

        /// <inheritdoc />
        public Task<bool> RevokeRefreshToken(string token)
        {
            using (var connection = ConnectionMultiplexer.Connect(this.redisConnectionString))
            {
                var db = connection.GetDatabase(this.redisDb);
                return Task.FromResult(db.KeyDelete(this.GetRedisRefreshKey(token)));
            }
        }

        /// <inheritdoc />
        public Task<AccessTicket> ValidateAccessToken(string token)
        {
            using (var connection = ConnectionMultiplexer.Connect(this.redisConnectionString))
            {
                var db = connection.GetDatabase(this.redisDb);
                var data = db.StringGet(this.GetRedisAccessKey(token));
                return data.HasValue
                           ? Task.FromResult(((byte[])data).DeserializeFromAkka<AccessTicket>(this.system))
                           : Task.FromResult<AccessTicket>(null);
            }
        }

        /// <inheritdoc />
        public Task<RefreshTicket> ValidateRefreshToken(string token)
        {
            var watch = new Stopwatch();
            watch.Start();

            using (var connection = ConnectionMultiplexer.Connect(this.redisConnectionString))
            {
                var db = connection.GetDatabase(this.redisDb);
                var redisRefreshKey = this.GetRedisRefreshKey(token);
                var data = db.StringGet(redisRefreshKey);
                db.KeyDelete(redisRefreshKey);
                return data.HasValue
                           ? Task.FromResult(((byte[])data).DeserializeFromAkka<RefreshTicket>(this.system))
                           : Task.FromResult<RefreshTicket>(null);
            }
        }

        /// <summary>
        /// Tests host against IP address
        /// </summary>
        /// <param name="host">The host name</param>
        /// <returns>A value indicating whether the provided host is a pure IP-v4</returns>
        private static bool IsIpAddress(string host)
        {
            string ipPattern = @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b";
            return Regex.IsMatch(host, ipPattern);
        }

        /// <summary>
        /// Creates the redis key from access token
        /// </summary>
        /// <param name="token">The token</param>
        /// <returns>The redis key</returns>
        private string GetRedisAccessKey(string token)
        {
            return $"{this.tokenKeyPrefix}Access:{token}";
        }

        /// <summary>
        /// Creates the redis key from refresh token
        /// </summary>
        /// <param name="token">The token</param>
        /// <returns>The redis key</returns>
        private string GetRedisRefreshKey(string token)
        {
            return $"{this.tokenKeyPrefix}Refresh:{token}";
        }
    }
}