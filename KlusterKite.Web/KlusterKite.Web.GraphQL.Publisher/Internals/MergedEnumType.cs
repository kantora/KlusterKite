// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MergedEnumType.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The merged type representing some enum value
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.Internals
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using global::GraphQL;
    using global::GraphQL.Types;

    using KlusterKite.API.Client;
    using KlusterKite.Web.GraphQL.Publisher.GraphTypes;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The merged type representing some enum value
    /// </summary>
    internal class MergedEnumType : MergedType
    {
        /// <summary>
        /// The api enum type.
        /// </summary>
        private readonly ApiEnumType apiEnumType;

        /// <summary>
        /// Initializes a new instance of the <see cref="MergedEnumType"/> class.
        /// </summary>
        /// <param name="apiEnumType">
        /// The api enum type.
        /// </param>
        /// <param name="provider">
        /// The provider.
        /// </param>
        public MergedEnumType(ApiEnumType apiEnumType, ApiProvider provider)
            : base(apiEnumType.TypeName)
        {
            this.apiEnumType = apiEnumType;
            this.Provider = provider;
        }

        /// <inheritdoc />
        public override string ComplexTypeName
            => $"{EscapeName(this.Provider.Description.ApiName)}_{EscapeName(this.OriginalTypeName)}";

        /// <summary>
        /// Gets the field provider
        /// </summary>
        public ApiProvider Provider { get; }

        /// <inheritdoc />
        public override IGraphType ExtractInterface(ApiProvider provider, NodeInterface nodeInterface)
        {
            if (this.Provider != provider)
            {
                return null;
            }

            return this.GenerateGraphType(null, null);
        }

        /// <inheritdoc />
        public override string GetInterfaceName(ApiProvider provider)
        {
            if (this.Provider != provider)
            {
                return null;
            }

            return this.ComplexTypeName;
        }

        /// <inheritdoc />
        public override async ValueTask<object> ResolveAsync(IResolveFieldContext context)
        {
            // Provider serializes null enum properties as JValue.CreateNull
            // (a non-null JToken whose .Value is null). Returning that JToken
            // to GraphQL.NET's EnumerationGraphType.Serialize lets it stringify
            // as "" and then fail "Error trying to resolve field '<name>'."
            // INVALID_OPERATION. Unwrap to a real null so the framework treats
            // the field as nullable-with-no-value and emits null.
            var result = await base.ResolveAsync(context);
            if (result is JValue jv && jv.Value == null)
            {
                return null;
            }

            return result;
        }

        /// <inheritdoc />
        public override IGraphType GenerateGraphType(NodeInterface nodeInterface, List<TypeInterface> interfaces)
        {
            var graphType = new EnumerationGraphType
                                {
                                    Description = this.apiEnumType.Description,
                                    Name = this.ComplexTypeName
                                };
            foreach (var enumValue in this.apiEnumType.Values)
            {
                graphType.Add(
                    enumValue,
                    new Newtonsoft.Json.Linq.JValue(enumValue),
                    this.apiEnumType.Descriptions.TryGetValue(enumValue, out var description) ? description : null);
            }

            return graphType;
        }
    }
}