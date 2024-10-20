// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MergedApiRoot.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The merged api root description
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.Internals
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using GraphQLParser.AST;
    using global::GraphQL.Resolvers;
    using global::GraphQL.Types;

    using KlusterKite.API.Attributes.Authorization;
    using KlusterKite.API.Client;
    using KlusterKite.Security.Attributes;
    using KlusterKite.Security.Client;
    using KlusterKite.Web.GraphQL.Publisher.GraphTypes;

    using Newtonsoft.Json.Linq;
    using global::GraphQL;

    /// <summary>
    /// The merged api root description
    /// </summary>
    internal class MergedApiRoot : MergedObjectType
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MergedApiRoot"/> class.
        /// </summary>
        /// <param name="originalTypeName">
        /// The original type name.
        /// </param>
        public MergedApiRoot(string originalTypeName)
            : base(originalTypeName)
        {
        }

        /// <summary>
        /// Gets combined name from all provider
        /// </summary>
        public override string ComplexTypeName
        {
            get
            {
                if (this.Providers.Any())
                {
                    var providersNames =
                        this.Providers.Select(p => EscapeName(p.Provider.Description.ApiName))
                            .Distinct()
                            .OrderBy(s => s)
                            .ToArray();

                    return string.Join("_", providersNames);
                }

                return EscapeName(this.OriginalTypeName);
            }
        }

        /// <summary>
        /// Gets the list of declared mutations
        /// </summary>
        public Dictionary<string, MergedField> Mutations { get; } = new Dictionary<string, MergedField>();

        /// <summary>
        /// Gets or sets the node searcher
        /// </summary>
        public NodeSearcher NodeSearher { get; internal set; }

        /// <inheritdoc />
        public override MergedObjectType Clone()
        {
            var clone = new MergedApiRoot(this.OriginalTypeName);
            this.FillWithMyFields(clone);
            foreach (var mutation in this.Mutations)
            {
                clone.Mutations[mutation.Key] = mutation.Value.Clone();
            }

            return clone;
        }

        /// <inheritdoc />
        public override IGraphType ExtractInterface(ApiProvider provider, NodeInterface nodeInterface)
        {
            var extractInterface = (TypeInterface)base.ExtractInterface(provider, nodeInterface);
            extractInterface.AddField(this.CreateNodeField(nodeInterface, false));
            return extractInterface;
        }

        /// <inheritdoc />
        public override IGraphType GenerateGraphType(NodeInterface nodeInterface, List<TypeInterface> interfaces)
        {
            var graphType = (VirtualGraphType)base.GenerateGraphType(nodeInterface, null);
            var nodeFieldType = this.CreateNodeField(nodeInterface, true);
            graphType.AddField(nodeFieldType);
            if (interfaces != null)
            {
                foreach (var typeInterface in interfaces)
                {
                    typeInterface.AddImplementedType(this.ComplexTypeName, graphType);
                    graphType.AddResolvedInterface(typeInterface);
                }
            }
            return graphType;
        }

        /// <summary>
        /// Generate graph type for all registered mutations
        /// </summary>
        /// <returns>The mutations graph type</returns>
        public IObjectGraphType GenerateMutationType()
        {
            var fields = this.Mutations.Select(f => this.ConvertApiField(f, new MutationResolver(f.Value)));
            return new VirtualGraphType("Mutations", fields.ToList())
            {
                Description =
                               "The list of all detected mutations"
            };
        }

        /// <inheritdoc />
        public override string GetInterfaceName(ApiProvider provider)
        {
            return $"I{provider.Description.ApiName}";
        }

        /// <inheritdoc />
        public override async ValueTask<object> ResolveAsync(IResolveFieldContext context)
        {
            return await this.DoApiRequests(context, context.UserContext as RequestContext);
        }

        /// <summary>
        /// Creates the node searcher field for the graph type
        /// </summary>
        /// <param name="nodeInterface">The node interface</param>
        /// <param name="addResolver">Whether resolver should be defined for the field. Interface fields should not have resolvers.</param>
        /// <returns>The node field</returns>
        private FieldType CreateNodeField(NodeInterface nodeInterface, bool addResolver)
        {
            var nodeFieldType = new FieldType();
            nodeFieldType.Name = "_node";
            nodeFieldType.ResolvedType = nodeInterface;
            nodeFieldType.Description = "The node global searcher according to Relay specification";
            nodeFieldType.Arguments =
                new QueryArguments(
                    new QueryArgument(typeof(IdGraphType)) { Name = "id", Description = "The node global id" });
            if (addResolver)
            {
                nodeFieldType.Resolver = this.NodeSearher;
            }
            return nodeFieldType;
        }

        /// <summary>
        /// Creates an api requests to gather all data
        /// </summary>
        /// <param name="context">
        /// The request contexts
        /// </param>
        /// <param name="requestContext">
        /// The request Context.
        /// </param>
        /// <returns>
        /// The request data
        /// </returns>
        private async ValueTask<JObject> DoApiRequests(IResolveFieldContext context, RequestContext requestContext)
        {
            var taskList = new List<ValueTask<JObject>>();
            foreach (var provider in this.Providers.Select(fp => fp.Provider))
            {
                var request = this.GatherMultipleApiRequest(provider, context.FieldAst, context).ToList();
                if (request.Count > 0)
                {
                    taskList.Add(provider.GetData(request, requestContext));
                }
            }

            JObject data;
            if (taskList.Count == 0)
            {
                data = new JObject();
            }
            else
            {
                var responses = await Task.WhenAll(taskList.Select(t => t.AsTask()));
                var options = new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Merge,
                    MergeNullValueHandling = MergeNullValueHandling.Ignore
                };

                var response = responses.Aggregate(
                    new JObject(),
                    (seed, next) =>
                        {
                            seed.Merge(next, options);
                            return seed;
                        });
                data = this.ResolveData(context, response);
            }

            if (data.Property(GlobalIdPropertyName) != null)
            {
                data.Property(GlobalIdPropertyName).Value = new JArray();
            }
            else
            {
                data.Add(GlobalIdPropertyName, new JArray());
            }

            return data;
        }

        /// <summary>
        /// Resolves mutation requests
        /// </summary>
        private class MutationResolver : IFieldResolver
        {
            /// <summary>
            /// The mutation description
            /// </summary>
            private readonly MergedField mergedField;

            /// <summary>
            /// Mutation API provider
            /// </summary>
            private readonly ApiProvider provider;

            /// <summary>
            /// Initializes a new instance of the <see cref="MutationResolver"/> class.
            /// </summary>
            /// <param name="mergedField">
            /// The merged field.
            /// </param>
            public MutationResolver(MergedField mergedField)
            {
                this.mergedField = mergedField;
                this.provider = this.mergedField.Providers.First();
            }



            /// <summary>
            /// Resolves mutation value (sends request to API)
            /// </summary>
            /// <param name="context">
            /// The context.
            /// </param>
            /// <returns>
            /// The <see cref="object"/>.
            /// </returns>
            public virtual async ValueTask<object> ResolveAsync(IResolveFieldContext context)
            {
                var connectionMutationResultType = this.mergedField.Type as MergedConnectionMutationResultType;
                if (connectionMutationResultType != null)
                {
                    return
                        this.DoConnectionMutationApiRequests(
                            context,
                            context.UserContext as RequestContext,
                            connectionMutationResultType).Result;
                }

                var untypedMutationResultType = this.mergedField.Type as MergedUntypedMutationResult;
                if (untypedMutationResultType != null)
                {
                    return
                        this.DoUntypedMutationApiRequests(
                            context,
                            context.UserContext as RequestContext,
                            untypedMutationResultType).Result;
                }

                return await this.DoApiRequests(context, context.UserContext as RequestContext);
            }

            /// <summary>
            /// Creates an api requests to gather all data
            /// </summary>
            /// <param name="context">
            /// The request contexts
            /// </param>
            /// <param name="requestContext">
            /// The request Context.
            /// </param>
            /// <returns>
            /// The request data
            /// </returns>
            private async ValueTask<JObject> DoApiRequests(IResolveFieldContext context, RequestContext requestContext)
            {
                var request = new MutationApiRequest
                {
                    Arguments = context.FieldAst.Arguments.ToJson(context),
                    FieldName = this.mergedField.FieldName,
                    Fields =
                                          this.mergedField.Type.GatherSingleApiRequest(
                                              context.FieldAst,
                                              context).ToList()
                };

                var apiRequests = await this.provider.GetData(new List<ApiRequest> { request }, requestContext);
                return apiRequests;
            }

            /// <summary>
            /// Creates an api requests to gather all data
            /// </summary>
            /// <param name="context">
            /// The request contexts
            /// </param>
            /// <param name="requestContext">
            /// The request Context.
            /// </param>
            /// <param name="responseType">response type</param>
            /// <returns>
            /// The request data
            /// </returns>
            private async ValueTask<JObject> DoConnectionMutationApiRequests(
                IResolveFieldContext context,
                RequestContext requestContext,
                MergedConnectionMutationResultType responseType)
            {
                var arguments = context.FieldAst.Arguments.ToJson(context).Property("input")?.Value as JObject;

                var actionName = this.mergedField.FieldName?.Split('.').LastOrDefault();
                EnConnectionAction action;
                var originalApiField = this.mergedField.OriginalFields.Values.FirstOrDefault();

                if (Enum.TryParse(actionName, true, out action) && originalApiField != null
                    && !originalApiField.CheckAuthorization(requestContext, action))
                {
                    var severity = originalApiField.LogAccessRules.Any()
                                       ? originalApiField.LogAccessRules.Max(l => l.Severity)
                                       : EnSeverity.Trivial;

                    SecurityLog.CreateRecord(
                        EnSecurityLogType.OperationDenied,
                        severity,
                        context.UserContext as RequestContext,
                        "Unauthorized call to {ApiPath}",
                        context.FieldAst.Name);

                    var emptyResponse = new JObject
                                            {
                                                {
                                                    "clientMutationId",
                                                    arguments?.Property("clientMutationId")?.ToObject<string>()
                                                }
                                            };
                    return emptyResponse;
                }

                var edgeType = responseType.EdgeType;
                var nodeType = responseType.EdgeType.ObjectType;
                var requestedFields = new List<ApiRequest>();
                var idSubRequestRequest = new List<ApiRequest>
                                              {
                                                  new ApiRequest
                                                      {
                                                          FieldName =
                                                              nodeType.KeyField.FieldName,
                                                          Alias = "_id"
                                                      }
                                              };
                var idRequestRequest = new ApiRequest
                {
                    Alias = "_idRequest",
                    FieldName = "result",
                    Fields = idSubRequestRequest
                };
                requestedFields.Add(idRequestRequest);

                var topFields =
                    GetRequestedFields(context.FieldAst.SelectionSet, context, this.mergedField.Type).ToList();

                var nodeRequests = topFields.Where(f => f.Name == "node" || f.Name == "edge").ToList();

                foreach (var nodeRequest in nodeRequests)
                {
                    var nodeAlias = nodeRequest.Alias.Name.StringValue ?? nodeRequest.Name.StringValue;
                    switch (nodeRequest.Name.StringValue)
                    {
                        case "node":
                            var nodeFields = nodeType.GatherSingleApiRequest(nodeRequest, context).ToList();
                            nodeFields.Add(new ApiRequest { Alias = "_id", FieldName = nodeType.KeyField.FieldName });
                            requestedFields.Add(
                                new ApiRequest { Alias = nodeAlias, FieldName = "result", Fields = nodeFields });
                            break;
                        case "edge":
                            var edgeFields = new List<ApiRequest>();
                            foreach (var edgeNodeRequests in
                                GetRequestedFields(nodeRequest.SelectionSet, context, edgeType)
                                    .Where(f => f.Name == "node"))
                            {
                                edgeFields.AddRange(
                                    nodeType.GatherSingleApiRequest(edgeNodeRequests, context).Select(
                                        f =>
                                            {
                                                f.Alias =
                                                    $"{edgeNodeRequests.Alias.Name ?? edgeNodeRequests.Name}_{f.Alias ?? f.FieldName}";
                                                return f;
                                            }));
                            }

                            edgeFields.Add(new ApiRequest { Alias = "_id", FieldName = nodeType.KeyField.FieldName });
                            requestedFields.Add(
                                new ApiRequest { Alias = nodeAlias, FieldName = "result", Fields = edgeFields });

                            break;
                    }
                }

                if (responseType.ErrorType != null)
                {
                    var errorsRequest = topFields.Where(f => f.Name == "errors");
                    foreach (var field in errorsRequest)
                    {
                        requestedFields.Add(
                            new ApiRequest
                            {
                                FieldName = "errors",
                                Alias = field.Alias.Name.ToString(),
                                Fields =
                                        responseType.ErrorType.GatherSingleApiRequest(field, context)
                                            .ToList()
                            });
                    }
                }

                var request = new MutationApiRequest
                {
                    Arguments = arguments,
                    FieldName = this.mergedField.FieldName,
                    Fields = requestedFields
                };

                var data = await this.provider.GetData(new List<ApiRequest> { request }, requestContext);
                if (data != null)
                {
                    var mutation = (ApiMutation)this.mergedField.OriginalFields[this.provider.Description.ApiName];
                    var treePath = mutation.Path.Take(mutation.Path.Count - 1).ToList();

                    var parentGlobalId = new JArray(treePath.Select(r => new JObject { { "f", r.FieldName } }));
                    data.Add(GlobalIdPropertyName, parentGlobalId);

                    var elementRequest = mutation.Path.LastOrDefault();
                    if (elementRequest != null)
                    {
                        var localRequest = new JObject { { "f", elementRequest.FieldName } };
                        data.Add(RequestPropertyName, localRequest);
                    }
                }

                data?.Add("clientMutationId", arguments?.Property("clientMutationId")?.ToObject<string>());
                return data;
            }

            /// <summary>
            /// Creates an api requests to gather all data
            /// </summary>
            /// <param name="context">
            /// The request contexts
            /// </param>
            /// <param name="requestContext">
            /// The request Context.
            /// </param>
            /// <param name="responseType">
            /// The response type
            /// </param>
            /// <returns>
            /// The request data
            /// </returns>
            private async ValueTask<JObject> DoUntypedMutationApiRequests(
                IResolveFieldContext context,
                RequestContext requestContext,
                MergedUntypedMutationResult responseType)
            {
                var topFields =
                    GetRequestedFields(context.FieldAst.SelectionSet, context, this.mergedField.Type).ToList();
                var requestedFields = new List<ApiRequest>();

                foreach (var topField in topFields.Where(f => f.Name == "result"))
                {
                    requestedFields.AddRange(responseType.OriginalReturnType.GatherSingleApiRequest(topField, context));
                }

                var arguments = context.FieldAst.Arguments.ToJson(context).Property("input")?.Value as JObject;

                var originalApiField = this.mergedField.OriginalFields.Values.FirstOrDefault();
                if (originalApiField != null
                    && !originalApiField.CheckAuthorization(requestContext, EnConnectionAction.Query))
                {
                    var severity = originalApiField.LogAccessRules.Any()
                                       ? originalApiField.LogAccessRules.Max(l => l.Severity)
                                       : EnSeverity.Trivial;

                    SecurityLog.CreateRecord(
                        EnSecurityLogType.OperationDenied,
                        severity,
                        context.UserContext as RequestContext,
                        "Unauthorized call to {ApiPath}",
                        context.FieldAst.Name);

                    var emptyResponse = new JObject
                                            {
                                                {
                                                    "clientMutationId",
                                                    arguments?.Property("clientMutationId")?.ToObject<string>()
                                                }
                                            };
                    return emptyResponse;
                }

                var request = new MutationApiRequest
                {
                    Arguments = arguments,
                    FieldName = this.mergedField.FieldName,
                    Fields = requestedFields
                };

                var data = await this.provider.GetData(new List<ApiRequest> { request }, requestContext);
                data?.Add("clientMutationId", arguments?.Property("clientMutationId")?.ToObject<string>());
                return data;
            }
        }
    }
}