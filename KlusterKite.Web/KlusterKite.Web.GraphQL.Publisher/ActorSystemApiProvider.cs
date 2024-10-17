// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ActorSystemApiProvider.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Provides access to api in whole cluster
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Akka.Actor;
    
    using KlusterKite.API.Client;
    using KlusterKite.API.Client.Messages;
    using KlusterKite.Core;
    using KlusterKite.Security.Attributes;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Provides access to api in whole cluster 
    /// </summary>
    public class ActorSystemApiProvider : ApiProvider
    {
        /// <summary>
        /// The random number generator
        /// </summary>
        private readonly Random random = new Random();

        /// <summary>
        /// Gets or sets the request timeout
        /// </summary>
        private readonly TimeSpan? timeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorSystemApiProvider"/> class.
        /// </summary>
        /// <param name="description">
        /// The API description.
        /// </param>
        /// <param name="system">
        /// The actor system.
        /// </param>
        public ActorSystemApiProvider(ApiDescription description, ActorSystem system)
        {
            this.timeout = ConfigurationUtils.GetRestTimeout(system);
            this.Description = description;
        }

        /// <summary>
        /// Gets the endpoints.
        /// </summary>
        public ConcurrentDictionary<Address, IActorRef> Endpoints { get; } = new ConcurrentDictionary<Address, IActorRef>();

        /// <inheritdoc />
        public override async ValueTask<JObject> GetData(List<ApiRequest> requests, RequestContext context)
        {
            var endpoint = this.GetEndpoint();
            if (endpoint == null)
            {
                return null;
            }

            var mutations = requests.OfType<MutationApiRequest>().ToList();
            if (mutations.Count > 0)
            {
                var result = new JObject();
                foreach (var mutation in mutations)
                {
                    mutation.Context = context;
                    
                    var midResult = await endpoint.Ask<SurrogatableJObject>(mutation, this.timeout);
                    result.Merge((JObject)midResult);
                }

                return result;
            }

            var query = new QueryApiRequest { Context = context, Fields = requests };
            return await endpoint.Ask<SurrogatableJObject>(query, this.timeout);
        }

        /// <summary>
        /// Selects the endpoint to solve the request
        /// </summary>
        /// <returns>The endpoint to solve</returns>
        private IActorRef GetEndpoint()
        {
            var endpoints = this.Endpoints.Values.ToList();
            if (endpoints.Count == 0)
            {
                return null;
            }

            var endpoint = endpoints[this.random.Next(0, endpoints.Count)];
            return endpoint;
        }
    }
}