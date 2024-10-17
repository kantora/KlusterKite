// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MergedUntypedMutationResult.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The type representing the mutation payload for the untyped mutation
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.Internals
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::GraphQL.Resolvers;
    using global::GraphQL.Types;

    using KlusterKite.API.Client;
    using KlusterKite.Web.GraphQL.Publisher.GraphTypes;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The type representing the mutation payload for the untyped mutation
    /// </summary>
    internal class MergedUntypedMutationResult : MergedObjectType
    {
        /// <summary>
        /// The edge type
        /// </summary>
        private readonly MergedApiRoot root;

        /// <summary>
        /// The original field description
        /// </summary>
        private readonly ApiField field;

        /// <summary>
        /// Initializes a new instance of the <see cref="MergedUntypedMutationResult"/> class.
        /// </summary>
        /// <param name="originalReturnType">
        /// The original mutation return type.
        /// </param>
        /// <param name="root">
        /// The root.
        /// </param>
        /// <param name="provider">
        /// The provider.
        /// </param>
        /// <param name="field">
        /// The original field description
        /// </param>
        public MergedUntypedMutationResult(
            MergedType originalReturnType,
            MergedApiRoot root,
            ApiProvider provider,
            ApiField field)
            : base(originalReturnType.OriginalTypeName)
        {
            this.OriginalReturnType = originalReturnType;
            this.root = root;
            this.field = field;
            this.Provider = provider;
        }

        /// <summary>
        /// Gets the provider
        /// </summary>
        public ApiProvider Provider { get; }

        /// <inheritdoc />
        public override string Description => this.OriginalReturnType.Description;

        /// <inheritdoc />
        public override string ComplexTypeName => $"{EscapeName(this.OriginalTypeName)}_MutationPayload";

        /// <summary>
        /// Gets the original return type
        /// </summary>
        public MergedType OriginalReturnType { get; }

        /// <inheritdoc />
        public override IGraphType GenerateGraphType(NodeInterface nodeInterface, List<TypeInterface> interfaces)
        {
            var graphType = new VirtualGraphType(this.ComplexTypeName);
            graphType.AddField(this.CreateField("result", this.OriginalReturnType, new ResultResolver(this.OriginalReturnType)));
            graphType.AddField(new FieldType { Name = "clientMutationId", ResolvedType = new StringGraphType(), Resolver = new MergedConnectionMutationResultType.ClientMutationIdIdResolver() });
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
            var mergedField = new MergedField(name, type, this.Provider, this.field, flags);
            return this.ConvertApiField(new KeyValuePair<string, MergedField>(name, mergedField), resolver);
        }

        /// <summary>
        /// The result resolver
        /// </summary>
        private class ResultResolver : IFieldResolver
        {
            /// <summary>
            /// The mutation result type
            /// </summary>
            private readonly MergedType resultType;

            /// <summary>
            /// Initializes a new instance of the <see cref="ResultResolver"/> class.
            /// </summary>
            /// <param name="resultType">
            /// The result type.
            /// </param>
            public ResultResolver(MergedType resultType)
            {
                this.resultType = resultType;
            }

            /// <inheritdoc />
            public ValueTask<object> ResolveAsync(global::GraphQL.IResolveFieldContext context)
            {
                var token = (context.Source as JObject)?.Property("result")?.Value;
                return new ValueTask<object>(this.resultType is MergedScalarType ? (token as JValue)?.Value : token);
            }
        }
    }
}
