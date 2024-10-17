// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeSearcher.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The node searcher
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.Internals
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using global::GraphQL.Resolvers;
    using global::GraphQL.Types;

    using JetBrains.Annotations;

    using KlusterKite.API.Client;
    using KlusterKite.Security.Attributes;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The node searcher
    /// </summary>
    internal class NodeSearcher : IFieldResolver
    {
        /// <summary>
        /// The api root.
        /// </summary>
        private readonly MergedApiRoot apiRoot;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeSearcher"/> class.
        /// </summary>
        /// <param name="apiRoot">
        /// The api Root.
        /// </param>
        public NodeSearcher(MergedApiRoot apiRoot)
        {
            this.apiRoot = apiRoot;
        }


        /// <inheritdoc />
        public ValueTask<object> ResolveAsync(global::GraphQL.IResolveFieldContext context)
        {
            return new ValueTask<object>(this.SearchNode(context));
        }

        /// <summary>
        /// Creates the nested request to get data
        /// </summary>
        /// <param name="globalId">The path to container</param>
        /// <param name="typesList">The list of nested types that corresponds the path</param>
        /// <param name="path">The generated path to the end element</param>
        /// <param name="tailRequests">The tail request for the found object data</param>
        /// <returns>The list of requests</returns>
        private static List<ApiRequest> CreateRequest(
            List<GlobalId> globalId,
            List<MergedType> typesList,
            List<string> path,
            List<ApiRequest> tailRequests)
        {
            for (var index = globalId.Count - 1; index >= 0; index--)
            {
                var element = globalId[index];
                var type = typesList[index];

                var args = element.Arguments ?? new JObject();
                if (element.Id != null)
                {
                    args.Add("id", element.Id);
                }

                ApiRequest request;
                if (type is MergedConnectionType)
                {
                    path.Add($"{element.FieldName}.items[0]");
                    request = new ApiRequest { FieldName = "items", Fields = tailRequests };

                    request = new ApiRequest
                                  {
                                      FieldName = element.FieldName,
                                      Arguments = args,
                                      Fields = new List<ApiRequest> { request }
                                  };
                }
                else
                {
                    path.Add(element.FieldName);
                    request = new ApiRequest
                                  {
                                      FieldName = element.FieldName,
                                      Arguments = args,
                                      Fields = tailRequests
                                  };
                }

                tailRequests = new List<ApiRequest> { request };
            }

            path.Reverse();
            return tailRequests;
        }

        /// <summary>
        /// Performs node search request
        /// </summary>
        /// <param name="context">The request context</param>
        /// <returns>The searched node</returns>
        private async Task<JObject> SearchNode(global::GraphQL.IResolveFieldContext context)
        {
            var arguments = context.FieldAst.Arguments.ToJson(context);
            var packedId = arguments?.Property("id")?.Value?.ToObject<string>();
            var serializedId = packedId?.UnpackGlobalId();
            if (string.IsNullOrWhiteSpace(serializedId))
            {
                return null;
            }

            List<GlobalId> globalId;
            try
            {
                globalId = JsonConvert.DeserializeObject<List<GlobalId>>(serializedId);
            }
            catch
            {
                return null;
            }

            var queue = new Queue<GlobalId>(globalId);
            MergedType mergedType = this.apiRoot;
            var typesList = new List<MergedType>();
            while (queue.Count > 0)
            {
                var field = queue.Dequeue();
                var objectType = mergedType as MergedObjectType;
                var connectionType = mergedType as MergedConnectionType;

                if (objectType != null)
                {
                    MergedField mergedField;
                    if (!objectType.Fields.TryGetValue(field.FieldName, out mergedField))
                    {
                        return null;
                    }

                    mergedType = mergedField.Type;
                    typesList.Add(mergedType);
                }
                else if (connectionType != null)
                {
                    MergedField mergedField;
                    if (!connectionType.ElementType.Fields.TryGetValue(field.FieldName, out mergedField))
                    {
                        return null;
                    }

                    mergedType = mergedField.Type;
                    typesList.Add(mergedType);
                }
                else
                {
                    return null;
                }
            }

            var finalType = (mergedType as MergedConnectionType)?.ElementType ?? mergedType as MergedObjectType;
            if (finalType == null)
            {
                return null;
            }

            List<string> path = new List<string>();

            var result = new JObject();
            if (finalType.Category == MergedObjectType.EnCategory.MultipleApiType)
            {
                if (typesList.Any(t => !(t is MergedObjectType)))
                {
                    return null;
                }
                
                foreach (var provider in this.apiRoot.Providers)
                {
                    var tailRequests =
                        finalType.GatherMultipleApiRequest(provider.Provider, context.FieldAst, context).ToList();
                    if (tailRequests.Count == 0)
                    {
                        continue;
                    }

                    path = new List<string>();
                    var requests = CreateRequest(globalId, typesList, path, tailRequests);
                    result.Merge(await provider.Provider.GetData(requests, context.UserContext as RequestContext));
                }
            }
            else
            {
                var tailRequests = finalType.GatherSingleApiRequest(context.FieldAst, context).ToList();
                var provider = finalType.Providers.First().Provider;
                
                tailRequests = CreateRequest(globalId, typesList, path, tailRequests);
                result = await provider.GetData(tailRequests, context.UserContext as RequestContext);
            }

            var item = result.SelectToken(string.Join(".", path)) as JObject;
            if (item == null)
            {
                return null;
            }

            item.Add("__resolvedType", finalType.ComplexTypeName);
            item.Add(MergedObjectType.GlobalIdPropertyName, JArray.Parse(serializedId));
            return item;
        }

        /// <summary>
        /// The class to deserialize global id
        /// </summary>
        private class GlobalId
        {
            /// <summary>
            /// Gets or sets the field arguments
            /// </summary>
            [JsonProperty("a")]
            [UsedImplicitly]
            public JObject Arguments { get; set; }

            /// <summary>
            /// Gets or sets the path to the connection
            /// </summary>
            [JsonProperty("f")]
            [UsedImplicitly]
            public string FieldName { get; set; }

            /// <summary>
            /// Gets or sets the id value
            /// </summary>
            [JsonProperty("id")]
            [UsedImplicitly]
            public JValue Id { get; set; }
        }
    }
}