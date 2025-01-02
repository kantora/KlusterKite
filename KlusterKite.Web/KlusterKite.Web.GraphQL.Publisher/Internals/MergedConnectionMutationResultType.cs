// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MergedConnectionMutationResultType.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The type representing the mutation payload for the connection mutation
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.Internals
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::GraphQL.Resolvers;
    using global::GraphQL.Types;
    using JetBrains.Annotations;

    using KlusterKite.API.Client;
    using KlusterKite.Web.GraphQL.Publisher.GraphTypes;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The type representing the mutation payload for the connection mutation
    /// </summary>
    internal class MergedConnectionMutationResultType : MergedObjectType
    {
        /// <summary>
        /// The edge type
        /// </summary>
        private readonly MergedApiRoot root;

        /// <summary>
        /// Initializes a new instance of the <see cref="MergedConnectionMutationResultType"/> class.
        /// </summary>
        /// <param name="nodeType">
        /// The node type.
        /// </param>
        /// <param name="root">
        /// The root.
        /// </param>
        /// <param name="errorType">
        /// The error type.
        /// </param>
        /// <param name="provider">
        /// The provider.
        /// </param>
        public MergedConnectionMutationResultType(
            MergedObjectType nodeType, 
            MergedApiRoot root,
            MergedType errorType,
            ApiProvider provider)
            : base(nodeType.OriginalTypeName)
        {
            this.EdgeType = new MergedEdgeType(nodeType.OriginalTypeName, provider, nodeType);
            this.root = root;
            this.ErrorType = errorType;
            this.Provider = provider;
        }

        /// <summary>
        /// Gets the field provider
        /// </summary>
        [UsedImplicitly]
        public ApiProvider Provider { get; }

        /// <summary>
        /// Gets the root type
        /// </summary>
        public MergedEdgeType EdgeType { get; }
        
        /// <summary>
        /// Gets the error type
        /// </summary>
        public MergedType ErrorType { get; }

        /// <inheritdoc />
        public override string ComplexTypeName => $"{this.OriginalTypeName}_NodeMutationPayload";

        /// <inheritdoc />
        public override IGraphType GenerateGraphType(NodeInterface nodeInterface, List<TypeInterface> interfaces)
        {
            var graphType = new VirtualGraphType(this.ComplexTypeName);
            graphType.AddField(
                new FieldType
                    {
                        Name = "deletedId",
                        ResolvedType = new IdGraphType(),
                        Resolver = new DeletedIdResolver()
                    });
            graphType.AddField(
                this.CreateField(
                    "node",
                    this.EdgeType.ObjectType,
                    new NodeResolver(this.EdgeType.ObjectType)));
            graphType.AddField(this.CreateField("edge", this.EdgeType));
            graphType.AddField(this.CreateField("errors", this.ErrorType, new ResultErrorsResolver()));
            graphType.AddField(
                new FieldType
                    {
                        Name = "clientMutationId",
                        ResolvedType = new StringGraphType(),
                        Resolver = new ClientMutationIdIdResolver()
                    });
            graphType.AddField(this.CreateField("api", this.root, this.root));
            return graphType;
        }



        /// <summary>
        /// Creates virtual field
        /// </summary>
        /// <param name="name">
        /// The field name
        /// </param>
        /// <param name="type">
        /// The field type
        /// </param>
        /// <param name="resolver">
        /// The field resolver
        /// </param>
        /// <param name="flags">
        /// The field flags.
        /// </param>
        /// <returns>
        /// The field
        /// </returns>
        private FieldType CreateField(string name, MergedType type, IFieldResolver resolver = null, EnFieldFlags flags = EnFieldFlags.None)
        {
            var mergedField = new MergedField(name, type, this.Provider, null, flags);
            return this.ConvertApiField(new KeyValuePair<string, MergedField>(name, mergedField), resolver);
        }

        /// <summary>
        /// Resolves data for node value
        /// </summary>
        internal class ClientMutationIdIdResolver : IFieldResolver
        {
            /// <inheritdoc />
            public ValueTask<object> ResolveAsync(global::GraphQL.IResolveFieldContext context)
            {
                return new ValueTask<object>(((context.Source as JObject)?.Property("clientMutationId")?.Value as JValue)?.Value);
            }
        }

        /// <summary>
        /// Resolves the "node" field of mutation payload
        /// </summary>
        private class NodeResolver : IFieldResolver
        {
            /// <summary>
            /// The original node type
            /// </summary>
            private readonly MergedObjectType nodeType;

            /// <summary>
            /// Initializes a new instance of the <see cref="NodeResolver"/> class.
            /// </summary>
            /// <param name="nodeType">
            /// The original node type.
            /// </param>
            public NodeResolver(MergedObjectType nodeType)
            {
                this.nodeType = nodeType;
            }

            /// <inheritdoc />
            public ValueTask<object> ResolveAsync(global::GraphQL.IResolveFieldContext context)
            {
                var source = context.Source as JObject;
                var resolvedSource = source?.Property(context.FieldAst.Alias?.Name?.StringValue ?? context.FieldAst.Name?.StringValue)?.Value as JObject;
                if (resolvedSource == null)
                {
                    return new ValueTask<object>(null);
                }

                return new ValueTask<object>(this.nodeType.ResolveData(context, resolvedSource, false));
            }
        }

        /// <summary>
        /// Resolves data for edge value
        /// </summary>
        private class ResultErrorsResolver : IFieldResolver
        {
            /// <inheritdoc />
            public ValueTask<object> ResolveAsync(global::GraphQL.IResolveFieldContext context)
            {
                return new ValueTask<object>(((JObject)context.Source)?.Property(context.FieldAst.Alias?.Name?.StringValue ?? context.FieldAst.Name?.StringValue)?.Value?.DeepClone());
            }
        } 

        /// <summary>
        /// Resolves data for node value
        /// </summary>
        private class DeletedIdResolver : IFieldResolver
        {
            /// <inheritdoc />
            public ValueTask<object> ResolveAsync(global::GraphQL.IResolveFieldContext context)
            {
                var contextSource = (JObject)context.Source;

                var value = contextSource.Property("__deletedId")?.Value;
                if (value == null)
                {
                    return new ValueTask<object>(null);
                }

                var globalId = contextSource.Property(GlobalIdPropertyName)?.Value?.DeepClone() as JArray;
                var request = contextSource.Property(RequestPropertyName)?.Value?.DeepClone() as JObject;
                if (globalId == null || request == null)
                {
                    return new ValueTask<object>(null); 
                }

                request.Add("id", value);
                globalId.Add(request);
                return new ValueTask<object>(globalId.PackGlobalId());
            }
        }
    }
}
