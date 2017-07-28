// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CollectionResolver.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Resolves requests to the object collection
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.API.Provider.Resolvers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading.Tasks;

    using KlusterKite.API.Attributes;
    using KlusterKite.API.Attributes.Authorization;
    using KlusterKite.API.Client;
    using KlusterKite.Security.Attributes;
    using KlusterKite.Security.Client;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Resolves requests to the object collection
    /// </summary>
    /// <typeparam name="T">The type of node</typeparam>
    /// <remarks>
    /// <see cref="CollectionResolver{T}"/> uses <see cref="ObjectResolver{T}"/> in it's static initialization. 
    /// So <see cref="ObjectResolver{T}"/> cannot use <see cref="CollectionResolver{T}"/> in it's static initialization to avoid deadlock
    /// </remarks>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType",
        Justification = "Making use of static properties in generic classes")]
    internal class CollectionResolver<T> : IResolver
        where T : class
    {
        /// <summary>
        /// The initialization lock to make it thread-safe
        /// </summary>
        protected static readonly object LockObject = new object();

        /// <summary>
        /// The filter checks pre-created expressions
        /// </summary>
        private static readonly Dictionary<string, Func<JProperty, Expression>> FilterChecks =
            new Dictionary<string, Func<JProperty, Expression>>();

        /// <summary>
        /// The filter checks pre-created expressions
        /// </summary>
        private static readonly ParameterExpression FilterSourceParameter = Expression.Parameter(typeof(T));

        /// <summary>
        /// The available sorting conditions
        /// </summary>
        private static readonly Dictionary<string, SortingCondition> SortingConditions =
            new Dictionary<string, SortingCondition>();

        /// <summary>
        /// A value indicating whether the type initialization process was completed
        /// </summary>
        private static bool isInitialized;

        /// <summary>
        /// Initializes static members of the <see cref="CollectionResolver{T}"/> class.
        /// </summary>
        static CollectionResolver()
        {
            FilterType = new ApiObjectType($"{ApiDescriptionAttribute.GetTypeName(typeof(T))}_Filter");
            SortType = new ApiEnumType($"{ApiDescriptionAttribute.GetTypeName(typeof(T))}_Sort");

            var param = Expression.Parameter(typeof(T));
            GetIdValue =
                Expression.Lambda<Func<T, object>>(
                    Expression.Convert(Expression.Property(param, NodeMetaData.KeyProperty), typeof(object)),
                    param).Compile();

            InitializeType();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CollectionResolver{T}"/> class.
        /// </summary>
        public CollectionResolver()
        {
            switch (NodeMetaData.ScalarType)
            {
                case EnScalarType.None:
                    this.NodeResolver = new ObjectResolver<T>();
                    break;
                case EnScalarType.DateTime:
                    this.NodeResolver = new DateTimeResolver();
                    break;
                default:
                    this.NodeResolver = new ScalarResolver<T>();
                    break;
            }
        }

        /// <summary>
        /// Gets the type to describe the collection filter argument
        /// </summary>
        public static ApiObjectType FilterType { get; }

        /// <summary>
        /// Gets a function to get id value from entity
        /// </summary>
        public static Func<T, object> GetIdValue { get; }

        /// <summary>
        /// Gets the type to describe the collection sort argument element
        /// </summary>
        public static ApiEnumType SortType { get; }

        /// <inheritdoc />
        public IResolver NodeResolver { get; }

        /// <summary>
        /// Gets the node metadata
        /// </summary>
        protected static TypeMetadata NodeMetaData { get; } = TypeMetadata.GenerateTypeMetadata(
            typeof(T),
            new DeclareFieldAttribute());

        /// <summary>
        /// Gets the generated arguments
        /// </summary>
        /// <returns>The list of arguments</returns>
        public static IEnumerable<ApiField> GetArguments()
        {
            if (FilterType.Fields.Count > 2)
            {
                yield return
                    FilterType.CreateField(
                        "filter",
                        EnFieldFlags.CanBeUsedInInput | EnFieldFlags.Queryable | EnFieldFlags.IsTypeArgument);
            }

            if (SortType.Values.Any())
            {
                const EnFieldFlags SortFlags =
                    EnFieldFlags.CanBeUsedInInput | EnFieldFlags.Queryable | EnFieldFlags.IsTypeArgument
                    | EnFieldFlags.IsArray;
                yield return ApiField.Object("sort", SortType.TypeName, SortFlags);
            }

            if (NodeMetaData.KeyProperty != null)
            {
                var keyMetadata = TypeMetadata.GenerateTypeMetadata(
                    NodeMetaData.KeyProperty.PropertyType,
                    NodeMetaData.KeyProperty.GetCustomAttribute<PublishToApiAttribute>());
                if (keyMetadata.ScalarType != EnScalarType.None)
                {
                    yield return
                        ApiField.Scalar(
                            "id",
                            keyMetadata.ScalarType,
                            EnFieldFlags.CanBeUsedInInput | EnFieldFlags.Queryable | EnFieldFlags.IsTypeArgument);
                }
            }

            yield return
                ApiField.Scalar(
                    "limit",
                    EnScalarType.Integer,
                    EnFieldFlags.CanBeUsedInInput | EnFieldFlags.Queryable | EnFieldFlags.IsTypeArgument);
            yield return
                ApiField.Scalar(
                    "offset",
                    EnScalarType.Integer,
                    EnFieldFlags.CanBeUsedInInput | EnFieldFlags.Queryable | EnFieldFlags.IsTypeArgument);
        }

        /// <inheritdoc />
        public ApiType GetElementType()
        {
            return NodeMetaData.ScalarType == EnScalarType.None ? ObjectResolver<T>.GeneratedType : null;
        }

        /// <inheritdoc />
        public IEnumerable<ApiField> GetTypeArguments()
        {
            return GetArguments();
        }

        /// <inheritdoc />
        public async Task<JToken> ResolveQuery(
            object source,
            ApiRequest request,
            ApiField apiField,
            RequestContext context,
            JsonSerializer argumentsSerializer,
            Action<Exception> onErrorCallback)
        {
            var arguments = (JObject)request.Arguments;
            var id = arguments?.Property("id");
            var filterArgument = arguments?.Property("filter")?.Value as JObject;
            var sortArgument = arguments?.Property("sort")?.Value as JArray;
            var limit = (int?)(arguments?.Property("limit")?.Value as JValue);
            var offset = (int?)(arguments?.Property("offset")?.Value as JValue);

            var filter = CreateFilter(filterArgument, id);
            var sort = sortArgument != null ? this.CreateSort(sortArgument) : null;

            var items = await this.GetQueryResult(source, request, filter, sort, limit, offset);
            if (items == null)
            {
                onErrorCallback?.Invoke(new Exception("Source is not a node connection"));
                return JValue.CreateNull();
            }

            SetLog(request, apiField, context, EnConnectionAction.Query);

            var result = new JObject();
            var fields = request.Fields.GroupBy(f => f.Alias ?? f.FieldName).Select(
                g =>
                {
                    var f = g.First();
                    if (f.Fields == null)
                    {
                        return f;
                    }

                    return new ApiRequest
                    {
                        Alias = f.Alias,
                        Arguments = f.Arguments,
                        FieldName = f.FieldName,
                        Fields = g.SelectMany(sr => sr.Fields).ToList()
                    };
                });

            foreach (var requestField in fields)
            {
                switch (requestField.FieldName)
                {
                    case "count":
                        result.Add(requestField.Alias ?? requestField.FieldName, new JValue(items.Count));
                        break;
                    case "items":
                        {
                            var itemsValue =
                                await new SimpleCollectionResolver(this.NodeResolver).ResolveQuery(
                                    items.Items,
                                    requestField,
                                    apiField,
                                    context,
                                    argumentsSerializer,
                                    onErrorCallback);
                            result.Add(requestField.Alias ?? requestField.FieldName, itemsValue);
                        }

                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Sets the operation log
        /// </summary>
        /// <param name="request">
        /// The initial request
        /// </param>
        /// <param name="apiField">
        /// The connection field
        /// </param>
        /// <param name="context">
        /// The request context
        /// </param>
        /// <param name="action">
        /// The action performed
        /// </param>
        protected static void SetLog(
            ApiRequest request,
            ApiField apiField,
            RequestContext context,
            EnConnectionAction action)
        {
            if (apiField.LogAccessRules == null || !apiField.LogAccessRules.Any())
            {
                return;
            }

            var rule =
                apiField.LogAccessRules.OrderByDescending(r => r.Severity)
                    .FirstOrDefault(r => r.ConnectionActions.HasFlag(action));

            if (rule == null)
            {
                return;
            }

            var operationGranted = EnSecurityLogType.OperationGranted;
            switch (action)
            {
                case EnConnectionAction.Create:
                    operationGranted = EnSecurityLogType.DataCreateGranted;
                    break;
                case EnConnectionAction.Update:
                    operationGranted = EnSecurityLogType.DataUpdateGranted;
                    break;
                case EnConnectionAction.Delete:
                    operationGranted = EnSecurityLogType.DataDeleteGranted;
                    break;
            }

            SecurityLog.CreateRecord(
                operationGranted,
                rule.Severity,
                context,
                rule.LogMessage,
                ((JObject)request.Arguments).ToString(Formatting.None));
        }

        /// <summary>
        /// Getting the query result
        /// </summary>
        /// <param name="source">The data source</param>
        /// <param name="request">The original request</param>
        /// <param name="filter">Filtering expression</param>
        /// <param name="sort">Sorting expression</param>
        /// <param name="limit">The maximum number of elements</param>
        /// <param name="offset">The number of the first element</param>
        /// <returns>The query result</returns>
        protected virtual Task<QueryResult<T>> GetQueryResult(
            object source,
            ApiRequest request,
            Expression<Func<T, bool>> filter,
            IEnumerable<SortingCondition> sort,
            int? limit,
            int? offset)
        {
            var queryable = source as IQueryable<T> ?? (source as IEnumerable<T>)?.AsQueryable();
            if (queryable == null)
            {
                return null;
            }

            if (filter != null)
            {
                queryable = queryable.Where(filter);
            }

            var result = new QueryResult<T> { Count = queryable.Count() };
            if (sort != null)
            {
                queryable = queryable.ApplySorting(sort);
            }

            if (offset.HasValue)
            {
                queryable = queryable.Skip(offset.Value);
            }

            if (limit.HasValue)
            {
                queryable = queryable.Take(limit.Value);
            }

            result.Items = queryable;
            return Task.FromResult(result);
        }

        /// <summary>
        /// Adds a new filter condition
        /// </summary>
        /// <param name="name">The name of condition</param>
        /// <param name="expression">The expression generator</param>
        /// <param name="field">The field to check</param>
        /// <param name="description">The filter description</param>
        private static void AddFilterExpression(
            string name,
            Func<JProperty, Expression> expression,
            ApiField field,
            string description)
        {
            FilterChecks[name] = expression;

            description = string.Format(description, field.Name);
            var filterDescription = !string.IsNullOrWhiteSpace(field.Description)
                                        ? $"{description}, {field.Name}: {field.Description}"
                                        : description;

            if (field.ScalarType != EnScalarType.None)
            {
                FilterType.Fields.Add(
                    ApiField.Scalar(
                        name,
                        field.ScalarType,
                        EnFieldFlags.Queryable | EnFieldFlags.CanBeUsedInInput,
                        description: filterDescription));
            }
            else
            {
                FilterType.Fields.Add(
                    ApiField.Object(
                        name,
                        field.TypeName,
                        EnFieldFlags.Queryable | EnFieldFlags.CanBeUsedInInput,
                        description: filterDescription));
            }
        }

        /// <summary>
        /// Creates filter expression by filter request
        /// </summary>
        /// <param name="filter">The filter request</param>
        /// <param name="id">The id argument</param>
        /// <returns>The filter expression</returns>
        private static Expression<Func<T, bool>> CreateFilter(JObject filter, JProperty id)
        {
            var filterCheck = filter != null ? CreateFilterPart(filter) : null;
            var idCheck = id != null ? FilterChecks[NodeMetaData.KeyPropertyName](id) : null;

            var check = filterCheck != null && idCheck != null
                            ? Expression.And(filterCheck, idCheck)
                            : filterCheck ?? idCheck;

            return check == null 
                ? null 
                : Expression.Lambda<Func<T, bool>>(check, FilterSourceParameter);
        }

        /// <summary>
        /// Creates part of of filter expression according to json description
        /// </summary>
        /// <param name="filterProperty">The filter json description</param>
        /// <returns>The expression</returns>
        private static Expression CreateFilterPart(JObject filterProperty)
        {
            Expression left = Expression.Constant(true);

            foreach (var prop in filterProperty.Properties())
            {
                Func<JProperty, Expression> check;
                if (FilterChecks.TryGetValue(prop.Name, out check))
                {
                    left = Expression.And(left, check(prop));
                }
            }

            return left;
        }

        /// <summary>
        /// Performs final type initialization
        /// </summary>
        private static void InitializeType()
        {
            if (isInitialized)
            {
                return;
            }

            lock (LockObject)
            {
                if (isInitialized)
                {
                    return;
                }

                isInitialized = true;

                var nodeType = ObjectResolver<T>.GeneratedType;
                var realFields = ObjectResolver<T>.DeclaredFields;
                var sortableFields = nodeType.Fields.Where(f => f.Flags.HasFlag(EnFieldFlags.IsSortable));
                foreach (var sortableField in sortableFields)
                {
                    SortingConditions[$"{sortableField.Name}_asc"] =
                        new SortingCondition(realFields[sortableField.Name].Name, SortingCondition.EnDirection.Asc);
                    SortingConditions[$"{sortableField.Name}_desc"] =
                        new SortingCondition(realFields[sortableField.Name].Name, SortingCondition.EnDirection.Desc);
                }

                SortType.Values.AddRange(SortingConditions.Keys);

                FilterChecks["OR"] = prop =>
                    {
                        var subFilters = prop.Value as JArray;
                        if (subFilters == null)
                        {
                            return Expression.Constant(true);
                        }

                        Expression or = Expression.Constant(false);
                        or = subFilters.Children()
                            .OfType<JObject>()
                            .Aggregate(or, (current, subFilter) => Expression.Or(current, CreateFilterPart(subFilter)));

                        return or;
                    };

                FilterType.Fields.Add(
                    FilterType.CreateField(
                        "OR",
                        description: "Combine filter conditions with logic \"OR\"",
                        flags: EnFieldFlags.Queryable | EnFieldFlags.CanBeUsedInInput));

                FilterChecks["AND"] = prop =>
                    {
                        var subFilters = prop.Value as JArray;
                        if (subFilters == null)
                        {
                            return Expression.Constant(true);
                        }

                        Expression and = Expression.Constant(true);
                        and = subFilters.Children()
                            .OfType<JObject>()
                            .Aggregate(
                                and,
                                (current, subFilter) => Expression.And(current, CreateFilterPart(subFilter)));

                        return and;
                    };

                FilterType.Fields.Add(
                    FilterType.CreateField(
                        "AND",
                        description: "Combine filter conditions with logic \"AND\"",
                        flags: EnFieldFlags.Queryable | EnFieldFlags.CanBeUsedInInput));

                var filterableFields = nodeType.Fields.Where(f => f.Flags.HasFlag(EnFieldFlags.IsFilterable));

                var stringContains = typeof(string).GetMethod("Contains");
                var stringStartsWith = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
                var stringEndsWith = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });
                var stringToLower = typeof(string).GetMethod("ToLower", new Type[0]);

                foreach (var filterableField in filterableFields)
                {
                    var property = realFields[filterableField.Name] as PropertyInfo;
                    if (property == null)
                    {
                        continue;
                    }

                    var propertyExpression = Expression.Property(FilterSourceParameter, property);

                    Func<JProperty, Expression> createConstant =
                        prop => Expression.Constant(prop.Value.ToObject(property.PropertyType), property.PropertyType);

                    AddFilterExpression(
                        filterableField.Name,
                        prop => Expression.Equal(propertyExpression, createConstant(prop)),
                        filterableField,
                        "{0} exactly equals to the parameter");
                    AddFilterExpression(
                        $"{filterableField.Name}_not",
                        prop => Expression.NotEqual(propertyExpression, createConstant(prop)),
                        filterableField,
                        "{0} not equals to the parameter");

                    switch (filterableField.ScalarType)
                    {
                        case EnScalarType.Float:
                        case EnScalarType.Decimal:
                        case EnScalarType.Integer:
                        case EnScalarType.DateTime:
                            AddFilterExpression(
                                $"{filterableField.Name}_lt",
                                prop => Expression.LessThan(propertyExpression, createConstant(prop)),
                                filterableField,
                                "{0} is less then the parameter");
                            AddFilterExpression(
                                $"{filterableField.Name}_lte",
                                prop => Expression.LessThanOrEqual(propertyExpression, createConstant(prop)),
                                filterableField,
                                "{0} is less then or equal to the parameter");
                            AddFilterExpression(
                                $"{filterableField.Name}_gt",
                                prop => Expression.GreaterThan(propertyExpression, createConstant(prop)),
                                filterableField,
                                "{0} is greater then the parameter");
                            AddFilterExpression(
                                $"{filterableField.Name}_gte",
                                prop => Expression.GreaterThanOrEqual(propertyExpression, createConstant(prop)),
                                filterableField,
                                "{0} is greater then or equal to the parameter");
                            break;
                        case EnScalarType.String:
                            var checkNull = Expression.NotEqual(Expression.Constant(null), propertyExpression);
                            var propertyToLower = Expression.Call(propertyExpression, stringToLower);
                            AddFilterExpression(
                                $"{filterableField.Name}_in",
                                prop => Expression.And(checkNull, Expression.Call(createConstant(prop), stringContains, propertyExpression)),
                                filterableField,
                                "{0} is a substring of the parameter");
                            AddFilterExpression(
                                $"{filterableField.Name}_not_in",
                                prop =>
                                    Expression.And(checkNull, Expression.Not(Expression.Call(createConstant(prop), stringContains, propertyExpression))),
                                filterableField,
                                "{0} is not a substring of the parameter");
                            AddFilterExpression(
                                $"{filterableField.Name}_contains",
                                prop => Expression.And(checkNull, Expression.Call(propertyExpression, stringContains, createConstant(prop))),
                                filterableField,
                                "{0} contains the parameter as substring");
                            AddFilterExpression(
                                $"{filterableField.Name}_not_contains",
                                prop =>
                                    Expression.And(checkNull, Expression.Not(Expression.Call(propertyExpression, stringContains, createConstant(prop)))),
                                filterableField,
                                "{0} doesn't contain the parameter as substring");
                            AddFilterExpression(
                                $"{filterableField.Name}_starts_with",
                                prop => Expression.And(checkNull, Expression.Call(propertyExpression, stringStartsWith, createConstant(prop))),
                                filterableField,
                                "{0} starts with the parameter value");
                            AddFilterExpression(
                                $"{filterableField.Name}_not_starts_with",
                                prop =>
                                    Expression.And(checkNull, Expression.Not(Expression.Call(propertyExpression, stringStartsWith, createConstant(prop)))),
                                filterableField,
                                "{0} doesn't start with the parameter value");
                            AddFilterExpression(
                                $"{filterableField.Name}_ends_with",
                                prop => Expression.And(checkNull, Expression.Call(propertyExpression, stringEndsWith, createConstant(prop))),
                                filterableField,
                                "{0} ends with the parameter value");
                            AddFilterExpression(
                                $"{filterableField.Name}_not_ends_with",
                                prop =>
                                    Expression.And(checkNull, Expression.Not(Expression.Call(propertyExpression, stringEndsWith, createConstant(prop)))),
                                filterableField,
                                "{0} doesn't end with the parameter value");

                            AddFilterExpression(
                                $"{filterableField.Name}_l",
                                prop => Expression.And(checkNull, Expression.Equal(propertyToLower, createConstant(prop))),
                                filterableField,
                                "{0} lower-cased equals the parameter");

                            AddFilterExpression(
                                $"{filterableField.Name}_l_not",
                                prop => Expression.And(checkNull, Expression.NotEqual(propertyToLower, createConstant(prop))),
                                filterableField,
                                "{0} lower-cased doesn't equal the parameter");

                            AddFilterExpression(
                                $"{filterableField.Name}_l_in",
                                prop => Expression.And(checkNull, Expression.Call(createConstant(prop), stringContains, propertyToLower)),
                                filterableField,
                                "{0} lower-cased is a substring of the parameter");
                            AddFilterExpression(
                                $"{filterableField.Name}_l_not_in",
                                prop =>
                                    Expression.And(checkNull, Expression.Not(Expression.Call(createConstant(prop), stringContains, propertyToLower))),
                                filterableField,
                                "{0} lower-cased is not a substring of the parameter");
                            AddFilterExpression(
                                $"{filterableField.Name}_l_contains",
                                prop => Expression.And(checkNull, Expression.Call(propertyToLower, stringContains, createConstant(prop))),
                                filterableField,
                                "{0} lower-cased contains the parameter as substring");
                            AddFilterExpression(
                                $"{filterableField.Name}_l_not_contains",
                                prop =>
                                    Expression.And(checkNull, Expression.Not(Expression.Call(propertyToLower, stringContains, createConstant(prop)))),
                                filterableField,
                                "{0} lower-cased doesn't contain the parameter as substring");
                            AddFilterExpression(
                                $"{filterableField.Name}_l_starts_with",
                                prop => Expression.And(checkNull, Expression.Call(propertyToLower, stringStartsWith, createConstant(prop))),
                                filterableField,
                                "{0} lower-cased starts with the parameter value");
                            AddFilterExpression(
                                $"{filterableField.Name}_l_not_starts_with",
                                prop =>
                                    Expression.And(checkNull, Expression.Not(Expression.Call(propertyToLower, stringStartsWith, createConstant(prop)))),
                                filterableField,
                                "{0} lower-cased doesn't start with the parameter value");
                            AddFilterExpression(
                                $"{filterableField.Name}_l_ends_with",
                                prop => Expression.And(checkNull, Expression.Call(propertyToLower, stringEndsWith, createConstant(prop))),
                                filterableField,
                                "{0} lower-cased ends with the parameter value");
                            AddFilterExpression(
                                $"{filterableField.Name}_l_not_ends_with",
                                prop =>
                                    Expression.And(checkNull, Expression.Not(Expression.Call(propertyToLower, stringEndsWith, createConstant(prop)))),
                                filterableField,
                                "{0} lower-cased doesn't end with the parameter value");
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Creates the list of sorting conditions by sorting request
        /// </summary>
        /// <param name="arguments">The sorting request</param>
        /// <returns>The list of sorting conditions</returns>
        private IEnumerable<SortingCondition> CreateSort(JArray arguments)
        {
            var sortArgs = arguments.ToObject<string[]>();
            foreach (var sort in sortArgs)
            {
                SortingCondition condition;
                if (SortingConditions.TryGetValue(sort, out condition))
                {
                    yield return condition;
                }
            }
        }
    }
}