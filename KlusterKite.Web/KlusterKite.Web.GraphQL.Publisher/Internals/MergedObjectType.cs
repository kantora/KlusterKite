// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MergedObjectType.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The merged api type description
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.GraphQL.Publisher.Internals
{
    using System;
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

    using Newtonsoft.Json.Linq;
    using global::GraphQL;
    using System.Threading.Tasks;

    /// <summary>
    /// The merged api type description
    /// </summary>
    internal class MergedObjectType : MergedFieldedType
    {
        /// <summary>
        /// Gets the name of the internal pre-calculated field to store globalId
        /// </summary>
        internal const string GlobalIdPropertyName = "__newGlobalId";

        /// <summary>
        /// Gets the name of the internal pre-calculated field to store request data for the current object
        /// </summary>
        internal const string RequestPropertyName = "_localRequest";

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
                    var providersNames =
                        this.Providers.Select(
                                p => $"{EscapeName(p.Provider.Description.ApiName)}_{EscapeName(p.FieldType.TypeName)}")
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
        /// Gets the globalId from source data and object id value
        /// </summary>
        /// <param name="source">The source data</param>
        /// <param name="id">The id value</param>
        /// <returns>The global id</returns>
        public static JArray GetGlobalId(JObject source, JToken id)
        {
            var parent = source.Parent;
            JArray parentGlobalId = null;
            var selfRequest = source.Property(RequestPropertyName)?.Value as JObject;
            while (parent != null)
            {
                parentGlobalId = (parent as JObject)?.Property(GlobalIdPropertyName)?.Value as JArray;
                if (selfRequest == null)
                {
                    selfRequest = (parent as JObject)?.Property(RequestPropertyName)?.Value as JObject;
                }

                if (parentGlobalId != null)
                {
                    break;
                }

                parent = parent.Parent;
            }

            if (source.Property(GlobalIdPropertyName) == null && selfRequest != null)
            {
                var globalId = parentGlobalId != null ? new JArray(parentGlobalId) : new JArray();
                var selfPart = (JObject)selfRequest.DeepClone();
                if (id != null)
                {
                    selfPart.Add("id", id);
                }

                globalId.Add(selfPart);
                return globalId;
            }

            return null;
        }

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
        public override IGraphType ExtractInterface(ApiProvider provider, NodeInterface nodeInterface)
        {
            var fieldProvider = this.providers.FirstOrDefault(fp => fp.Provider == provider);
            if (fieldProvider == null)
            {
                return null;
            }

            var fields =
                this.Fields.Where(f => f.Value.Providers.Any(fp => fp == provider))
                    .Select(this.ConvertApiField)
                    .ToList();

            var idField = fields.FirstOrDefault(f => f.Name == "id");
            if (idField != null)
            {
                idField.Name = "_id";
            }

            fields.Insert(0, new FieldType { Name = "id", ResolvedType = new IdGraphType() });
            var apiInterface =
                new TypeInterface(
                    this.GetInterfaceName(provider),
                    fieldProvider.FieldType.Description);

            foreach (var field in fields)
            {
                apiInterface.AddField(field);
            }

            return apiInterface;
        }

        /// <inheritdoc />
        public override string GetInterfaceName(ApiProvider provider)
        {
            var fieldProvider = this.providers.FirstOrDefault(fp => fp.Provider == provider);
            return fieldProvider == null
                ? null :
                $"I{EscapeName(provider.Description.ApiName)}_{EscapeName(fieldProvider.FieldType.TypeName)}";
        }

        /// <inheritdoc />
        public override IEnumerable<string> GetPossibleFragmentTypeNames()
        {
            foreach (var typeName in base.GetPossibleFragmentTypeNames())
            {
                yield return typeName;
            }

            foreach (var fieldProvider in this.Providers)
            {
                yield return this.GetInterfaceName(fieldProvider.Provider);
            }

            yield return "Node";
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
        public IEnumerable<ApiRequest> GatherMultipleApiRequest(
            ApiProvider provider,
            GraphQLField contextFieldAst,
            IResolveFieldContext context)
        {
            if (this.KeyField != null && this.KeyField.Providers.Contains(provider))
            {
                yield return new ApiRequest { Alias = "_id", FieldName = this.KeyField.FieldName };
            }

            var requestedFields =
                GetRequestedFields(contextFieldAst.SelectionSet, context, this).ToList();
            var usedFields =
                requestedFields.Join(
                    this.Fields.Where(f => f.Value.Providers.Any(fp => fp == provider)),
                    s => s.Name.StringValue,
                    fp => fp.Key,
                    (s, fp) => new { Ast = s, Field = fp.Value }).ToList();

            foreach (var usedField in usedFields)
            {
                var apiField = usedField.Field.OriginalFields[provider.Description.ApiName];
                if (apiField != null
                    && !apiField.CheckAuthorization(context.UserContext.ToRequestContext(), EnConnectionAction.Query))
                {
                    var severity = apiField.LogAccessRules.Any()
                                       ? apiField.LogAccessRules.Max(l => l.Severity)
                                       : EnSeverity.Trivial;

                    SecurityLog.CreateRecord(
                        EnSecurityLogType.OperationDenied,
                        severity,
                        context.UserContext.ToRequestContext(),
                        "Unauthorized call to {ApiPath}",
                        $"{contextFieldAst.Name}.{apiField.Name}");

                    continue;
                }

                var request = new ApiRequest
                {
                    Arguments = usedField.Ast.Arguments?.ToJson(context),
                    Alias = usedField.Ast.Alias?.Name.StringValue ?? usedField.Ast.Name?.StringValue,
                    FieldName = usedField.Field.FieldName
                };
                var endType = usedField.Field.Type as MergedObjectType;

                request.Fields = endType?.Category == EnCategory.MultipleApiType
                                     ? endType.GatherMultipleApiRequest(provider, usedField.Ast, context).ToList()
                                     : usedField.Field.Type.GatherSingleApiRequest(usedField.Ast, context).ToList();

                yield return request;
            }
        }

        /// <inheritdoc />
        public override IGraphType GenerateGraphType(NodeInterface nodeInterface, List<TypeInterface> interfaces)
        {
            var graphType = nodeInterface.PossibleTypes.FirstOrDefault(t => t.Name == this.ComplexTypeName) as VirtualGraphType;

            if (graphType == null)
            {
                graphType = (VirtualGraphType)base.GenerateGraphType(nodeInterface, null);
                var idField = graphType.Fields.FirstOrDefault(f => f.Name == "id");
                if (idField != null)
                {
                    idField.Name = "_id";
                }
                graphType.AddField(new FieldType { Name = "id", ResolvedType = new IdGraphType(), Resolver = new GlobalIdResolver() });
                graphType.AddResolvedInterface(nodeInterface);
                nodeInterface.AddImplementedType(this.ComplexTypeName, graphType);
            }

            if (interfaces != null)
            {
                foreach (var typeInterface in interfaces)
                {
                    if (typeInterface.PossibleTypes.FirstOrDefault(t => t.Name == this.ComplexTypeName) == null)
                    {
                        typeInterface.AddImplementedType(this.ComplexTypeName, graphType);
                        graphType.AddResolvedInterface(typeInterface);
                    }
                }
            }

            return graphType;
        }

        /// <inheritdoc />
        public override async ValueTask<object> ResolveAsync(IResolveFieldContext context)
        {
            var resolve = await base.ResolveAsync(context) as JObject;
            if (resolve == null)
            {
                return null;
            }

            return this.ResolveData(context, resolve);
        }

        /// <summary>
        /// Resolves parent data
        /// </summary>
        /// <param name="context">
        /// The request context
        /// </param>
        /// <param name="source">
        /// The parent data
        /// </param>
        /// <param name="setLocalRequest">
        /// A value indicating whether to set "local request" bread crumbs
        /// </param>
        /// <returns>
        /// The resolved data
        /// </returns>
        public virtual JObject ResolveData(IResolveFieldContext context, JObject source, bool setLocalRequest = true)
        {
            if (setLocalRequest)
            {
                var localRequest = new JObject { { "f", context.FieldDefinition.Name } };
                if (context.Arguments != null && context.Arguments.Any())
                {
                    var args =
                        context.Arguments
                            .OrderBy(p => p.Key)
                            .ToDictionary(p => p.Key, p => p.Value);
                    if (args.Any())
                    {
                        var argumentsValue = JObject.FromObject(args);
                        localRequest.Add("a", argumentsValue);
                    }
                }

                source.Add(RequestPropertyName, localRequest);
            }

            // generating self globalId data
            var globalId = GetGlobalId(source, source.Property("_id")?.Value);
            if (globalId != null)
            {
                source.Add(GlobalIdPropertyName, globalId);
            }

            return source;
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
        /// Resolves value for global id
        /// </summary>
        private class GlobalIdResolver : IFieldResolver
        {

            /// <inheritdoc />
            public ValueTask<object> ResolveAsync(IResolveFieldContext context)
            {
                var id = (context.Source as JObject)?.Property(GlobalIdPropertyName)?.Value;

                if (id == null)
                {
                    // relay doesn't like null id
                    return ValueTask.FromResult((object)new JObject { { "empty", Guid.NewGuid().ToString("N") } }.PackGlobalId());
                }

                return ValueTask.FromResult((object)id.PackGlobalId());
            }
        }
    }
}