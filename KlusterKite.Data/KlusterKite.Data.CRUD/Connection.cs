// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Connection.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The base connection using data actors
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Data.CRUD
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    using Akka.Actor;

    using KlusterKite.API.Client;
    using KlusterKite.Core.Utils;
    using KlusterKite.Data.CRUD.ActionMessages;
    using KlusterKite.Data.CRUD.Exceptions;
    using KlusterKite.LargeObjects.Client;
    using KlusterKite.Security.Attributes;

    /// <summary>
    /// The base connection using data actors
    /// </summary>
    /// <typeparam name="TObject">
    /// The type of entity
    /// </typeparam>
    /// <typeparam name="TId">
    /// The type of object id
    /// </typeparam>
    /// TODO: remove TId type parameter and recover it from type data
    public class Connection<TObject, TId> : INodeConnection<TObject>
        where TObject : class, IObjectWithId<TId>, new()
    {
        /// <inheritdoc />
        public Connection(ActorSystem actorSystem, string dataActorPath, TimeSpan? timeout, RequestContext context)
        {
            this.System = actorSystem;
            this.DataActorPath = dataActorPath;
            this.Timeout = timeout;
            this.Context = context;
        }

        /// <summary>
        /// Gets the request context
        /// </summary>
        protected RequestContext Context { get; }

        /// <summary>
        /// Gets the path to the data actor
        /// </summary>
        protected string DataActorPath { get; }

        /// <summary>
        /// Gets the actor system
        /// </summary>
        protected ActorSystem System { get; }

        /// <summary>
        /// Gets the request timeout
        /// </summary>
        protected TimeSpan? Timeout { get; }

        /// <inheritdoc />
        public async Task<MutationResult<TObject>> Create(TObject newNode)
        {
            var request =
                new CrudActionMessage<TObject, TId>
                    {
                        ActionType = EnActionType.Create,
                        Data = newNode,
                        RequestContext = this.Context
                    };

            var result = await this.System.ActorSelection(this.DataActorPath)
                             .Ask<CrudActionResponse<TObject>>(request, this.Timeout);
            return CreateResponse(result);
        }

        /// <inheritdoc />
        public async Task<MutationResult<TObject>> Delete(TId id)
        {
            var request =
                new CrudActionMessage<TObject, TId>
                    {
                        ActionType = EnActionType.Delete,
                        Id = id,
                        RequestContext = this.Context
                    };

            var result = await this.System.ActorSelection(this.DataActorPath)
                             .Ask<CrudActionResponse<TObject>>(request, this.Timeout);
            return CreateResponse(result);
        }

        /// <inheritdoc />
        public async Task<QueryResult<TObject>> Query(
            Expression<Func<TObject, bool>> filter,
            IEnumerable<SortingCondition> sort,
            int? limit,
            int? offset,
            ApiRequest apiRequest)
        {
            var sortingConditions = sort?.ToList() ?? new List<SortingCondition>();

            if (sortingConditions.Count == 0)
            {
                offset = null;
            }

            ApiRequest combinedRequest = null;
            if (apiRequest != null)
            {
                combinedRequest = new ApiRequest
                                      {
                                          Fields =
                                              apiRequest.Fields.Where(f => f.FieldName == "items")
                                                  .SelectMany(f => f.Fields)
                                                  .ToList()
                                      };
            }

            var request = new CollectionRequest<TObject>
                              {
                                  Count = limit,
                                  Skip = offset,
                                  Filter = filter,
                                  Sort = sortingConditions,
                                  AcceptAsParcel = true,
                                  ApiRequest = combinedRequest
                              };

            var parcel = await this.System.ActorSelection(this.DataActorPath)
                             .Ask<ParcelNotification>(request, this.Timeout);
            var result = (CollectionResponse<TObject>)await parcel.Receive(this.System);
            return new QueryResult<TObject> { Count = result.Count, Items = result.Items };
        }

        /// <inheritdoc />
        public async Task<MutationResult<TObject>> Update(TId id, TObject newNode, ApiRequest apiRequest)
        {
            var request =
                new CrudActionMessage<TObject, TId>
                    {
                        ActionType = EnActionType.Update,
                        Id = id,
                        Data = newNode,
                        RequestContext = this.Context,
                        ApiRequest = apiRequest
                    };

            var result = await this.System.ActorSelection(this.DataActorPath)
                             .Ask<CrudActionResponse<TObject>>(request, this.Timeout);
            return CreateResponse(result);
        }

        /// <inheritdoc />
        Task<MutationResult<TObject>> INodeConnection<TObject>.Delete(object id)
        {
            return this.Delete((TId)id);
        }

        /// <inheritdoc />
        Task<MutationResult<TObject>> INodeConnection<TObject>.Update(object id, TObject newNode, ApiRequest request)
        {
            return this.Update((TId)id, newNode, request);
        }

        /// <summary>
        /// Creates mutation response from actor response
        /// </summary>
        /// <param name="response">The actor response</param>
        /// <returns>The mutation response</returns>
        protected static MutationResult<TObject> CreateResponse(CrudActionResponse<TObject> response)
        {
            if (response.Data != null)
            {
                return new MutationResult<TObject> { Result = response.Data };
            }

            var errors = response.Exception.Match<List<ErrorDescription>>()           
                .With<EntityNotFoundException>(
                    e => new List<ErrorDescription> { new ErrorDescription("id", "not found") })
                .With<MutationException>(e => e.Errors)
                .ResultOrDefault(
                    e =>
                        {
                            var exception = e as Exception;
                            var errorDescription = new ErrorDescription(
                                "null",
                                exception != null ? $"{exception.Message}\n{exception.StackTrace}" : "unknown error");
                            return new List<ErrorDescription> { errorDescription };
                        });

            return new MutationResult<TObject> { Errors = errors };
        }
    }
}