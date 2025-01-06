// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MergedEdgeType.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The edge representative
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.Internals
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::GraphQL;
    using global::GraphQL.Resolvers;
    using global::GraphQL.Types;
    using GraphQLParser.AST;
    using KlusterKite.API.Client;
    using KlusterKite.Web.GraphQL.Publisher.GraphTypes;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The edge representative
    /// </summary>
    internal class MergedEdgeType : MergedType
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MergedEdgeType"/> class.
        /// </summary>
        /// <param name="originalTypeName">
        /// The original type name.
        /// </param>
        /// <param name="provider">
        /// The provider.
        /// </param>
        /// <param name="objectType">
        /// The end Type.
        /// </param>
        public MergedEdgeType(string originalTypeName, ApiProvider provider, MergedObjectType objectType) : base(originalTypeName)
        {
            this.ObjectType = objectType;
            this.Provider = provider;
        }

        /// <inheritdoc />
        public override string ComplexTypeName => $"{EscapeName(this.OriginalTypeName)}_Edge";

        /// <inheritdoc />
        public override string Description => $"The {this.ObjectType.ComplexTypeName} in a connected list\n {this.ObjectType.Description}";

        /// <summary>
        /// Gets the end type
        /// </summary>
        public MergedObjectType ObjectType { get; }

        /// <summary>
        /// Gets the field provider
        /// </summary>
        public ApiProvider Provider { get; }

        /// <inheritdoc />
        public override IGraphType GenerateGraphType(NodeInterface nodeInterface, List<TypeInterface> interfaces)
        {
            FieldType cursorField = new FieldType
            {
                Name = "cursor",
                ResolvedType = new StringGraphType(),
                //Resolver = new CursorResolver(),
                Description = "A value to use with paging positioning"
            };

            cursorField.Metadata[MetaDataTypeKey] = new MergedField(
                "cursor",
                new MergedScalarType(EnScalarType.String),
                this.Provider,
                null,
                description: "A value to use with paging positioning")
            {
                Resolver = new CursorResolver()
            };

            FieldType nodeField = new FieldType
            {
                Name = "node",
                ResolvedType = this.ObjectType.GenerateGraphType(nodeInterface, null)

            };

            nodeField.Metadata[MetaDataTypeKey] = new MergedField(
                "node",
                this.ObjectType,
                this.Provider,
                null,
                description: ObjectType.Description)
            {
                Resolver = new NodeResolver(this.ObjectType)
            };

            var fields = new List<FieldType>
                             {
                                 cursorField,
                                 nodeField
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
            var parentData = context.Source as JObject;
            if (parentData == null)
            {
                return null;
            }

            var edges = parentData.GetValue(context.FieldAst.Alias?.Name?.StringValue ?? context.FieldAst.Name.StringValue) as JArray;
            if (edges == null)
            {
                return null;
            }


            return edges.Select(obj => new EdgeValue { Node = obj as JObject });
        }

        private class EdgeValue
        {
            public string Cursor { get; set; }

            public JObject Node { get; set; }
        }

        /// <summary>
        /// The node resolver
        /// </summary>
        private class NodeResolver : IFieldResolver
        {
            /// <summary>
            /// The original node type
            /// </summary>
            private MergedObjectType originalType;

            /// <summary>
            /// Initializes a new instance of the <see cref="NodeResolver"/> class.
            /// </summary>
            /// <param name="originalType">
            /// The original type.
            /// </param>
            public NodeResolver(MergedObjectType originalType)
            {
                this.originalType = originalType;
            }


            /// <inheritdoc />
            public async ValueTask<object> ResolveAsync(IResolveFieldContext context)
            {
                var source = (context.Source as EdgeValue)?.Node;
                if (source == null)
                {
                    return null;
                }

                /*
                var fieldName = context.FieldAst.Alias?.Name?.StringValue ?? context.FieldAst.Name.StringValue;
                var filteredSource = new JObject();
                var prefix = $"{fieldName}_";
                foreach (var property in source.Properties().Where(p => p.Name.StartsWith(prefix)))
                {
                    filteredSource.Add(property.Name.Substring(prefix.Length), property.Value);
                }
                

                source.Add(fieldName, filteredSource);
                */

                return this.originalType.ResolveData(context, source, false);
            }
        }

        /// <summary>
        /// Resolves value for the edge cursor
        /// </summary>
        private class CursorResolver : IFieldResolver
        {
            public async ValueTask<object> ResolveAsync(IResolveFieldContext context)
            {
                return (context.Source as EdgeValue)?.Node.GetValue("_id")?.ToString();
            }
        }
    }
}