// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MergedFieldedType.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The api type with fields description
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.Internals
{
    using System.Collections.Generic;
    using System.Linq;

    using GraphQLParser.AST;
    using global::GraphQL.Resolvers;
    using global::GraphQL.Types;

    using KlusterKite.API.Attributes.Authorization;
    using KlusterKite.API.Client;
    using KlusterKite.Security.Attributes;
    using KlusterKite.Security.Client;
    using KlusterKite.Web.GraphQL.Publisher.GraphTypes;
    using global::GraphQL;

    /// <summary>
    /// The api type with fields description
    /// </summary>
    internal abstract class MergedFieldedType : MergedType
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MergedFieldedType"/> class.
        /// </summary>
        /// <param name="originalTypeName">
        /// The original type name.
        /// </param>
        protected MergedFieldedType(string originalTypeName)
            : base(originalTypeName)
        {
            this.Fields = new Dictionary<string, MergedField>();
        }

        /// <summary>
        /// Gets or sets the list of subfields
        /// </summary>
        public Dictionary<string, MergedField> Fields { get; protected set; }

        /// <summary>
        /// Gets the object's key field
        /// </summary>
        public MergedField KeyField { get; private set; }

        /// <summary>
        /// Initializes the internal state
        /// </summary>
        public void Initialize()
        {
            this.KeyField = this.Fields.Values.FirstOrDefault(f => f.Flags.HasFlag(EnFieldFlags.IsKey));
        }

        /// <inheritdoc />
        public override IEnumerable<ApiRequest> GatherSingleApiRequest(
            GraphQLField contextFieldAst,
            IResolveFieldContext context)
        {
            if (this.KeyField != null)
            {
                yield return new ApiRequest
                {
                    Alias = "__id",
                    FieldName = this.KeyField.FieldName
                };
            }

            foreach (var field in GetRequestedFields(contextFieldAst.SelectionSet, context, this))
            {
                if (field.Alias.Name == "__id")
                {
                    continue;
                }

                MergedField localField;
                if (field.Name == "id")
                {
                    continue;
                }

                if (field.Name == "__id" && this.KeyField != null)
                {
                    localField = this.KeyField;
                }
                else if (!this.Fields.TryGetValue(field.Name.StringValue, out localField))
                {
                    continue;
                }

                var apiField = localField.OriginalFields.Values.First();
                if (apiField != null
                    && !apiField.CheckAuthorization(context.UserContext as RequestContext, EnConnectionAction.Query))
                {
                    var severity = apiField.LogAccessRules.Any()
                                       ? apiField.LogAccessRules.Max(l => l.Severity)
                                       : EnSeverity.Trivial;

                    SecurityLog.CreateRecord(
                        EnSecurityLogType.OperationDenied,
                        severity,
                        context.UserContext as RequestContext,
                        "Unauthorized call to {ApiPath}",
                        $"{contextFieldAst.Name}.{apiField.Name}");
                    continue;
                }

                var request = new ApiRequest
                {
                    Arguments = field.Arguments.ToJson(context),
                    FieldName = localField.FieldName,
                    Alias = field.Alias.Name.StringValue ?? field.Name.StringValue,
                    Fields = localField.Type.GatherSingleApiRequest(field, context).ToList()
                };
                if (request.Fields.Count == 0)
                {
                    request.Fields = null;
                }

                yield return request;
            }
        }

        /// <inheritdoc />
        public override IGraphType GenerateGraphType(NodeInterface nodeInterface, List<TypeInterface> interfaces)
        {
            var fields = this.Fields.Select(this.ConvertApiField);
            var graphType = new VirtualGraphType(this.ComplexTypeName, fields.ToList()) { Description = this.Description };
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
        /// Creates <see cref="FieldType"/> from <see cref="ApiObjectType"/>
        /// </summary>
        /// <param name="description">
        /// The api field description
        /// </param>
        /// <returns>
        /// The <see cref="FieldType"/>
        /// </returns>
        protected FieldType ConvertApiField(KeyValuePair<string, MergedField> description)
        {
            return this.ConvertApiField(description, null);
        }

        /// <summary>
        /// Creates <see cref="FieldType"/> from <see cref="ApiObjectType"/>
        /// </summary>
        /// <param name="description">
        /// The api field description
        /// </param>
        /// <param name="resolver">
        /// The field resolver.
        /// </param>
        /// <returns>
        /// The <see cref="FieldType"/>
        /// </returns>
        protected FieldType ConvertApiField(KeyValuePair<string, MergedField> description, IFieldResolver resolver)
        {
            var field = new FieldType
            {
                Name = description.Key,
                Type = typeof(VirtualGraphType), // to workaround internal library checks
                Resolver = resolver,
                Description = description.Value.Description
            };
            field.Metadata[MetaDataTypeKey] = description.Value;
            return field;
        }
    }
}