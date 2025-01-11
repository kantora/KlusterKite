// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MergedConnectionType.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Defines the MergedConnectionType type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.Internals
{
    using System.Collections.Generic;
    using System.Linq;

    using GraphQLParser.AST;
    using global::GraphQL.Resolvers;
    using global::GraphQL.Types;

    using JetBrains.Annotations;

    using KlusterKite.API.Client;
    using KlusterKite.Web.GraphQL.Publisher.GraphTypes;

    using Newtonsoft.Json.Linq;
    using global::GraphQL;
    using System.Threading.Tasks;

    /// <summary>
    /// The connection representation
    /// </summary>
    internal class MergedConnectionType : MergedType
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MergedConnectionType"/> class.
        /// </summary>
        /// <param name="originalTypeName">
        /// The original type name.
        /// </param>
        /// <param name="provider">
        /// The provider.
        /// </param>
        /// <param name="elementType">
        /// The end Type.
        /// </param>
        public MergedConnectionType(string originalTypeName, ApiProvider provider, MergedObjectType elementType)
            : base(originalTypeName)
        {
            this.ElementType = elementType;
            this.Provider = provider;
            this.EdgeType = new MergedEdgeType(this.OriginalTypeName, provider, this.ElementType);
        }

        /// <inheritdoc />
        public override string ComplexTypeName => $"{EscapeName(this.OriginalTypeName)}_Connection";

        /// <inheritdoc />
        public override string Description
            => $"The list of connected {this.ElementType.ComplexTypeName}\n {this.ElementType.Description}";

        /// <summary>
        /// Gets the object end type
        /// </summary>
        public MergedObjectType ElementType { get; }

        /// <summary>
        /// Gets the type of the edge
        /// </summary>
        public MergedEdgeType EdgeType { get; }

        /// <summary>
        /// Gets the field provider
        /// </summary>
        public ApiProvider Provider { get; }

        /// <summary>
        /// Gets or sets the list of containing field argument names that came from type itself
        /// </summary>
        [NotNull]
        public List<string> TypedArgumentNames { get; set; } = new List<string>();

        /// <inheritdoc />
        public override IEnumerable<ApiRequest> GatherSingleApiRequest(
            GraphQLField contextFieldAst,
            IResolveFieldContext context)
        {
            foreach (var field in GetRequestedFields(contextFieldAst.SelectionSet, context, this))
            {
                switch (field.Name.StringValue)
                {
                    case "count":
                        yield return new ApiRequest { FieldName = "count", Alias = field.Alias?.Name?.StringValue };
                        break;
                    case "edges":
                        {
                            var fields = new List<ApiRequest>
                                             {
                                                 new ApiRequest { FieldName = this.ElementType.KeyField.FieldName, Alias = "_id" }
                                             };
                            foreach (var nodeRequest in
                                GetRequestedFields(field.SelectionSet, context, this.ElementType)
                                    .Where(f => f.Name == "node"))
                            {
                                fields.AddRange(
                                    this.ElementType.GatherSingleApiRequest(nodeRequest, context));

                                
                                fields.AddRange(
                                    this.ElementType.GatherSingleApiRequest(nodeRequest, context).Select(
                                        f =>
                                            {
                                                f.Alias =
                                                    $"{nodeRequest.Alias?.Name?.StringValue ?? nodeRequest.Name.StringValue}_{f.Alias ?? f.FieldName}";
                                                return f;
                                            }).ToList());
                                
                            }

                            yield return
                                new ApiRequest
                                {
                                    FieldName = "items",
                                    Alias = field.Alias?.Name?.StringValue ?? field.Name?.StringValue,
                                    Fields = fields
                                };
                        }

                        break;
                }
            }
        }

        /// <inheritdoc />
        public override IGraphType GenerateGraphType(NodeInterface nodeInterface, List<TypeInterface> interfaces)
        {
            FieldType countField = new FieldType
            {
                Name = "count",
                ResolvedType = new IntGraphType(),
                // Resolver = new CountResolver(),
                Description = "The total count of objects satisfying filter conditions"
            };

            countField.Metadata[MetaDataTypeKey] = new MergedField(
                "count",
                new MergedScalarType(EnScalarType.Integer),
                this.Provider,
                null,
                description: "The total count of objects satisfying filter conditions")
            {
                Resolver = new CountResolver()
            };

            FieldType edgesField = new FieldType
            {
                Name = "edges",
                Description = "The list of edges according to filtering and paging conditions",
                ResolvedType = new ListGraphType(this.ElementType.GenerateGraphType(nodeInterface, null)),
            };

            edgesField.Metadata[MetaDataTypeKey] = new MergedField(
                                                             "edges",
                                                             this.EdgeType,
                                                             this.Provider,
                                                             null,
                                                             EnFieldFlags.IsArray,
                                                             description: "The list of edges according to filtering and paging conditions");

            var fields = new List<FieldType>
                             {
                                 countField,
                                 edgesField
                             };

            var generateGraphType = new VirtualGraphType(this.ComplexTypeName, fields) { Description = this.Description };
            if (interfaces != null)
            {
                foreach (var typeInterface in interfaces)
                {
                    typeInterface.AddPossibleType(generateGraphType);
                    generateGraphType.AddResolvedInterface(typeInterface);
                }
            }

            return generateGraphType;
        }

        /// <inheritdoc />
        public override IGraphType ExtractInterface(ApiProvider provider, NodeInterface nodeInterface)
        {
            if (this.Provider != provider)
            {
                return null;
            }

            var nodeType = (ObjectGraphType)this.GenerateGraphType(nodeInterface, null);
            var apiInterface = new TypeInterface(this.GetInterfaceName(provider), this.Description);
            foreach (var field in nodeType.Fields)
            {
                apiInterface.AddField(field);
            }

            return apiInterface;
        }

        /// <inheritdoc />
        public override string GetInterfaceName(ApiProvider provider)
        {
            if (this.Provider != provider)
            {
                return null;
            }

            return $"I{this.ComplexTypeName}";
        }

        /// <inheritdoc />
        public override IEnumerable<string> GetPossibleFragmentTypeNames()
        {
            foreach (var typeName in base.GetPossibleFragmentTypeNames())
            {
                yield return typeName;
            }

            yield return this.GetInterfaceName(this.Provider);
        }

        /// <inheritdoc />
        public override async ValueTask<object> ResolveAsync(IResolveFieldContext context)
        {
            var source = await base.ResolveAsync(context) as JObject;
            if (source == null)
            {
                return null;
            }

            var localRequest = new JObject { { "f", context.FieldAst.Name.StringValue } };
            if (context.Arguments != null && context.Arguments.Any())
            {
                var args =
                    context.Arguments.Where(p => !this.TypedArgumentNames.Contains(p.Key))
                        .OrderBy(p => p.Key)
                        .ToDictionary(p => p.Key, p => p.Value.Value);
                if (args.Any())
                {
                    var argumentsValue = JObject.FromObject(args);
                    localRequest.Add("a", argumentsValue);
                }
            }

            source.Add(MergedObjectType.RequestPropertyName, localRequest);

            return source;
        }

        /// <summary>
        /// Resolves value for the edge cursor
        /// </summary>
        private class CountResolver : IFieldResolver
        {
            /// <inheritdoc />
            public async ValueTask<object> ResolveAsync(IResolveFieldContext context)
            {
                var source = context.Source as JObject;
                if (source == null)
                {
                    return null;
                }

                var count = source.GetValue(context.FieldAst.Alias?.Name?.StringValue ?? context.FieldAst.Name.StringValue);
                if (count == null)
                {
                    return null;
                }

                return int.Parse(count.ToString());
            }
        }

    }

    /// <summary>
    /// The syntehtic type to represent the result of the <seealso cref="MergedConnectionType.ResolveAsync(IResolveFieldContext)"/>
    /// </summary>
    internal class ConnectionResolve
    {
        /// <summary>
        /// Gets the total counnt of edges if requested
        /// </summary>
        public long? Count { get; init; }

        /// <summary>
        /// Gets the requested edges data
        /// </summary>
        public JArray Edges { get; init; }
    }


}