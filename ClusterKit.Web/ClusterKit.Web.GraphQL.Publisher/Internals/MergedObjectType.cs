// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MergedObjectType.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   The merged api type description
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.Web.GraphQL.Publisher.Internals
{
    using System.Collections.Generic;
    using System.Linq;

    using ClusterKit.API.Client;
    using ClusterKit.Web.GraphQL.Publisher.GraphTypes;

    using global::GraphQL.Language.AST;
    using global::GraphQL.Resolvers;
    using global::GraphQL.Types;

    /// <summary>
    /// The merged api type description
    /// </summary>
    internal class MergedObjectType : MergedFieldedType
    {
        /// <summary>
        /// the list of providers
        /// </summary>
        private readonly List<FieldProvider> providers = new List<FieldProvider>();

        /// <summary>
        /// Initializes a new instance of the <see cref="MergedObjectType"/> class.
        /// </summary>
        /// <param name="originalTypeName">
        /// The original type name.
        /// </param>
        public MergedObjectType(string originalTypeName)
            : base(originalTypeName)
        {
            this.Category = EnCategory.SingleApiType;
        }

        /// <summary>
        /// The field type category
        /// </summary>
        public enum EnCategory
        {
            /// <summary>
            /// This is object provided by some single api
            /// </summary>
            SingleApiType,

            /// <summary>
            /// This is object that is combined from multiple API providers (some fields provided by one API, some from other)
            /// </summary>
            MultipleApiType
        }

        /// <summary>
        /// Gets or sets the field category
        /// </summary>
        public EnCategory Category { get; set; }

        /// <summary>
        /// Gets combined name from all provider
        /// </summary>
        public override string ComplexTypeName
        {
            get
            {
                if (this.Providers.Any())
                {
                    var providersNames = this.Providers.Select(p => $"{EscapeName(p.Provider.Description.ApiName)}_{EscapeName(p.FieldType.TypeName)}")
                        .Distinct()
                        .OrderBy(s => s)
                        .ToArray();

                    return string.Join("_", providersNames);
                }

                return EscapeName(this.OriginalTypeName);
            }
        }

        /// <inheritdoc />
        public override string Description
            =>
                this.Providers.Any()
                    ? string.Join(
                        "\n",
                        this.Providers.Select(p => p.FieldType.Description).Distinct().OrderBy(s => s).ToArray())
                    : null;

        /// <summary>
        /// Gets or sets the list of providers
        /// </summary>
        public IEnumerable<FieldProvider> Providers => this.providers;

        /// <summary>
        /// Adds a provider to the provider list
        /// </summary>
        /// <param name="provider">The provider</param>
        public void AddProvider(FieldProvider provider)
        {
            this.providers.Add(provider);
        }

        /// <summary>
        /// Adds the list of providers to the provider list
        /// </summary>
        /// <param name="newProviders">The list of providers</param>
        public void AddProviders(IEnumerable<FieldProvider> newProviders)
        {
            this.providers.AddRange(newProviders);
        }

        /// <summary>
        /// Makes a duplicate of the current object
        /// </summary>
        /// <returns>The object duplicate</returns>
        public virtual MergedObjectType Clone()
        {
            var mergedObjectType = new MergedObjectType(this.OriginalTypeName);
            this.FillWithMyFields(mergedObjectType);
            return mergedObjectType;
        }

        /// <inheritdoc />
        public override IGraphType GenerateGraphType(NodeInterface nodeInterface)
        {
            var graphType = (VirtualGraphType)base.GenerateGraphType(nodeInterface);
            if (graphType.Fields.All(f => f.Name != "id"))
            {
                graphType.AddField(new FieldType
                                       {
                                           Name = "id",
                                           ResolvedType = new IdGraphType(),
                                           Resolver = new VirtualIdResolver(this)
                                       });
            }

            return graphType;
        }

        /// <summary>
        /// Fills the empty object with current objects fields
        /// </summary>
        /// <param name="shell">The empty object to fill</param>
        protected virtual void FillWithMyFields(MergedObjectType shell)
        {
            shell.AddProviders(this.providers);
            shell.Fields = this.Fields.ToDictionary(p => p.Key, p => p.Value.Clone());
            shell.Category = this.Category;
        }

        /// <summary>
        /// Gather request parameters for the specified api provider
        /// </summary>
        /// <param name="provider">
        /// The api provider
        /// </param>
        /// <param name="contextFieldAst">
        /// The request context
        /// </param>
        /// <param name="context">
        /// The resolve context.
        /// </param>
        /// <returns>
        /// The list of api requests
        /// </returns>
        protected IEnumerable<ApiRequest> GatherMultipleApiRequest(ApiProvider provider, Field contextFieldAst, ResolveFieldContext context)
        {
            var usedFields =
                GetRequestedFields(contextFieldAst.SelectionSet, context, this.ComplexTypeName)
                    .Join(
                        this.Fields.Where(f => f.Value.Providers.Any(fp => fp == provider)),
                        s => s.Name,
                        fp => fp.Key,
                        (s, fp) => new { Ast = s, Field = fp.Value })
                    .ToList();

            foreach (var usedField in usedFields)
            {
                var request = new ApiRequest
                {
                    Arguments = usedField.Ast.Arguments.ToJson(context),
                    Alias = usedField.Ast.Alias,
                    FieldName = usedField.Ast.Name
                };
                var endType = usedField.Field.Type as MergedObjectType;

                request.Fields = endType?.Category == EnCategory.MultipleApiType
                                     ? endType.GatherMultipleApiRequest(provider, usedField.Ast, context).ToList()
                                     : usedField.Field.Type.GatherSingleApiRequest(usedField.Ast, context).ToList();

                yield return request;
            }
        }

        /// <summary>
        /// Resolves value for virtual id of static classes
        /// </summary>
        private class VirtualIdResolver : IFieldResolver
        {
            /// <summary>
            /// The parent type
            /// </summary>
            private readonly MergedObjectType parentType;

            /// <summary>
            /// Initializes a new instance of the <see cref="VirtualIdResolver"/> class.
            /// </summary>
            /// <param name="parentType">
            /// The parent type.
            /// </param>
            public VirtualIdResolver(MergedObjectType parentType)
            {
                this.parentType = parentType;
            }

            /// <inheritdoc />
            public object Resolve(ResolveFieldContext context)
            {
                return this.parentType.ComplexTypeName;
            }
        }
    }
}