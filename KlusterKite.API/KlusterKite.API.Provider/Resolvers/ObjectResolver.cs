// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ObjectResolver.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Generic class to resolve requests to objects
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.API.Provider.Resolvers
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading.Tasks;

    using KlusterKite.API.Attributes;
    using KlusterKite.API.Client;
    using KlusterKite.Security.Attributes;
    using KlusterKite.Security.Client;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// The base class for <see cref="ObjectResolver{T}"/> just to make untyped methods
    /// </summary>
    public abstract class ObjectResolver : IResolver
    {
        /// <summary>
        /// Gets a value of specified field without arguments
        /// </summary>
        /// <param name="source">
        /// The source object
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="argumentsSerializer">
        /// The arguments serializer.
        /// </param>
        /// <param name="onErrorCallback">
        /// The on error callback.
        /// </param>
        /// <returns>The value</returns>
        internal delegate Task<object> GetMutationContainerDelegate(
            object source,
            RequestContext context,
            JsonSerializer argumentsSerializer,
            Action<Exception> onErrorCallback);

        /// <summary>
        /// Gets the list of errors occurred during initialization
        /// </summary>
        public abstract IEnumerable<string> Errors { get; }

        /// <summary>
        /// Gets the defined <see cref="ApiType"/>
        /// </summary>
        /// <returns>The api type</returns>
        public abstract ApiObjectType GetApiType();

        /// <inheritdoc />
        public IEnumerable<ApiField> GetTypeArguments()
        {
            yield break;
        }

        /// <inheritdoc />
        public abstract Task<JToken> ResolveQuery(
            object source,
            ApiRequest request,
            ApiField apiField,
            RequestContext context,
            JsonSerializer argumentsSerializer,
            Action<Exception> onErrorCallback);

        /// <inheritdoc />
        ApiType IResolver.GetElementType() => this.GetApiType();

        /// <summary>
        /// Creates an api root data from current type
        /// </summary>
        /// <param name="types">The list of all types used in api</param>
        /// <param name="argumentsSerializer">The configured JSON serializer to deserialize method arguments</param>
        /// <param name="mutationList">The list of all defined mutations</param>
        /// <param name="errors">The list of generated errors</param>
        internal abstract void CreateApiRoot(
            out List<ApiType> types,
            out JsonSerializer argumentsSerializer,
            out List<MutationDescription> mutationList,
            out List<string> errors);

        /// <summary>
        /// Generates mutations
        /// </summary>
        /// <param name="path">
        /// The path to current field
        /// </param>
        /// <param name="typesUsed">
        /// The types already used to avoid circular references
        /// </param>
        /// <param name="getContainerDelegate">
        /// The get source container.
        /// </param>
        /// <returns>
        /// The list of mutations
        /// </returns>
        internal abstract IEnumerable<MutationDescription> GenerateMutations(
            List<string> path,
            List<string> typesUsed,
            GetMutationContainerDelegate getContainerDelegate);

        /// <summary>
        /// Gets the list <see cref="ObjectResolver{T}"/> that a used to resolve the fields of current object
        /// </summary>
        /// <returns>The list of resolvers</returns>
        internal abstract IEnumerable<IResolver> GetDirectRelatedObjectResolvers();

        /// <summary>
        /// Gets the list <see cref="ApiType"/> that a used to resolve the fields of current object but are not correspond to real objects and does not have a resolver
        /// </summary>
        /// <returns>The list of resolvers</returns>
        internal abstract IEnumerable<ApiType> GetDirectRelatedVirtualTypes();

        /// <summary>
        /// Gets the names of type fields
        /// </summary>
        /// <returns>The names of type fields</returns>
        internal abstract Dictionary<MemberInfo, string> GetFieldsNames();

        /// <summary>
        /// Gets a value of specified nested field
        /// </summary>
        /// <param name="source">
        /// The source object
        /// </param>
        /// <param name="fieldNames">
        /// The nested field names
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="argumentsSerializer">
        /// The arguments serializer.
        /// </param>
        /// <param name="onErrorCallback">
        /// The on error callback.
        /// </param>
        /// <returns>The value</returns>
        internal abstract Task<Tuple<object, IResolver, ApiField>> GetNestedFieldValue(
            object source,
            Queue<string> fieldNames,
            RequestContext context,
            JsonSerializer argumentsSerializer,
            Action<Exception> onErrorCallback);

        /// <summary>
        /// The internal description of published mutation
        /// </summary>
        internal class MutationDescription
        {
            /// <summary>
            /// Gets or sets the mutation field
            /// </summary>
            public ApiField Field { get; set; }

            /// <summary>
            /// Gets the mutation virtual field name
            /// </summary>
            public string MutationName
                => string.Join(".", new List<string>(this.Path.Select(p => p?.FieldName)) { this.Field.Name });

            /// <summary>
            /// Gets or sets the path to mutation container
            /// </summary>
            public List<ApiRequest> Path { get; set; }

            /// <summary>
            /// Gets or sets the method to resolve property container
            /// </summary>
            public GetMutationContainerDelegate ResolveContainer { get; set; }

            /// <summary>
            /// Gets or sets the container resolver
            /// </summary>
            public IResolver Resolver { get; set; }

            /// <summary>
            /// Gets or sets the mutation type
            /// </summary>
            public ApiMutation.EnType Type { get; set; }

            /// <summary>
            /// Creates virtual mutation field to describe mutation
            /// </summary>
            /// <returns>The mutation field</returns>
            public ApiMutation CreateMutationField()
            {
                var field = ApiMutation.CreateFromField(this.Field, this.Type, this.Path);
                field.Name = this.MutationName;
                return field;
            }
        }

        /// <summary>
        /// The related type description
        /// </summary>
        protected class RelatedType
        {
            /// <summary>
            /// Gets or sets the related api type
            /// </summary>
            public ApiType ApiType { get; set; }

            /// <summary>
            /// Gets or sets the type resolver
            /// </summary>
            public IResolver Resolver { get; set; }

            /// <summary>
            /// Gets or sets the type
            /// </summary>
            public Type Type { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this type is connection type and should be hidden from API schema (but all related type will be used)
            /// </summary>
            public bool IsConnectionType { get; set; }
        }
    }

    /// <summary>
    /// Generic class to resolve requests to objects 
    /// </summary>
    /// <typeparam name="T">
    /// The type of object to resolve
    /// </typeparam>
    /// <remarks>
    /// <see cref="CollectionResolver{T}"/> uses <see cref="ObjectResolver{T}"/> in it's static initialization. 
    /// So <see cref="ObjectResolver{T}"/> cannot use <see cref="CollectionResolver{T}"/> in it's static initialization to avoid deadlock
    /// </remarks>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType",
        Justification = "Making use of static properties in generic classes")]

    // ReSharper disable once StyleCop.SA1402
    public class ObjectResolver<T> : ObjectResolver
    {
        /// <summary>
        /// The list of field descriptions
        /// </summary>
        private static readonly Dictionary<string, FieldDescription> Fields = new Dictionary<string, FieldDescription>();

        /// <summary>
        /// The list of generation errors
        /// </summary>
        private static readonly List<string> GenerationErrors = new List<string>();

        /// <summary>
        /// The initialization lock to make it thread-safe
        /// </summary>
        private static readonly object LockObject = new object();

        /// <summary>
        /// The list of related types
        /// </summary>
        private static readonly List<RelatedType> RelatedTypes = new List<RelatedType>();

        /// <summary>
        /// A value indicating whether the type initialization process was completed
        /// </summary>
        private static bool isInitialized;

        /// <summary>
        /// The current type name
        /// </summary>
        private static string typeName;

        /// <summary>
        /// Initializes static members of the <see cref="ObjectResolver{T}"/> class.
        /// </summary>
        static ObjectResolver()
        {
            var type = typeof(T);

            typeName = ApiDescriptionAttribute.GetTypeName(type);

            foreach (var f in GenerateTypeProperties())
            {
                Fields[f.Field.Name] = f;
            }

            foreach (var f in GenerateTypeMethods())
            {
                Fields[f.Field.Name] = f;
            }

            var apiObjectType = new ApiObjectType(typeName);
            apiObjectType.Fields.AddRange(Fields.Values.Where(f => !f.IsMutation).Select(f => f.Field));
            apiObjectType.Description = type.GetTypeInfo().GetCustomAttribute<ApiDescriptionAttribute>()?.Description;

            if (apiObjectType.Fields.Count == 0)
            {
                // GenerationErrors.Add("Object has no fields");
            }
            else
            {
                var keysCount = apiObjectType.Fields.Count(f => f.Flags.HasFlag(EnFieldFlags.IsKey));

                if (keysCount == 0)
                {
                    // GenerationErrors.Add("Object has no key");
                }
                else if (keysCount > 1)
                {
                    GenerationErrors.Add("Object has multiple keys");
                }
            }

            GeneratedType = apiObjectType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectResolver{T}"/> class.
        /// </summary>
        public ObjectResolver()
        {
            InitializeType();
        }

        /// <summary>
        /// Gets a value of objects property according to API request
        /// </summary>
        /// <param name="source">
        /// The source.
        /// </param>
        /// <param name="request">
        /// The api request.
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="argumentsSerializer">
        /// The arguments serializer.
        /// </param>
        /// <param name="onErrorCallback"> 
        /// The on error callback.
        /// </param>
        /// <returns>The value</returns>
        private delegate Task<object> PropertyValueGetter(
            T source,
            ApiRequest request,
            RequestContext context,
            JsonSerializer argumentsSerializer,
            Action<Exception> onErrorCallback);

        /// <summary>
        /// Gets the declared fields
        /// </summary>
        public static IReadOnlyDictionary<string, MemberInfo> DeclaredFields
            => Fields.ToImmutableDictionary(f => f.Key, f => f.Value.TypeMember);

        /// <summary>
        /// Gets the generated api type for typed argument
        /// </summary>
        public static ApiObjectType GeneratedType { get; }

        /// <summary>
        /// Gets the list of errors occurred during initialization
        /// </summary>
        public override IEnumerable<string> Errors => GenerationErrors.ToImmutableList();

        /// <inheritdoc />
        public override ApiObjectType GetApiType() => GeneratedType;

        /// <inheritdoc />
        public override async Task<JToken> ResolveQuery(
            object sourceUntyped,
            ApiRequest request,
            ApiField apiField,
            RequestContext context,
            JsonSerializer argumentsSerializer,
            Action<Exception> onErrorCallback)
        {
            var source = (T)sourceUntyped;
            if (request?.Fields == null || request.Fields.Count == 0)
            {
                return new JObject();
            }

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

            object idValue = null;
            foreach (var fieldRequest in fields)
            {
                var fieldName = fieldRequest.Alias ?? fieldRequest.FieldName;

                if (fieldRequest.FieldName == "__type")
                {
                    result.Add(fieldName, typeName);
                    continue;
                }

                if (!Fields.TryGetValue(fieldRequest.FieldName, out var field))
                {
                    onErrorCallback?.Invoke(new Exception($"Unknown field {fieldRequest.FieldName}"));
                    continue;
                }

                try
                {
                    object propertyValue;
                    try
                    {
                        propertyValue = await field.GetValue(
                                            source,
                                            fieldRequest,
                                            context,
                                            argumentsSerializer,
                                            onErrorCallback);
                    }
                    finally
                    {
                        if (field.Field.LogAccessRules.Any()
                            && !field.Metadata.GetFlags().HasFlag(EnFieldFlags.IsConnection))
                        {
                            if (idValue == null)
                            {
                                var key = Fields.Values.FirstOrDefault(f => f.Field.Flags.HasFlag(EnFieldFlags.IsKey));
                                if (key != null)
                                {
                                    idValue = await key.GetValue(
                                                  source,
                                                  fieldRequest,
                                                  context,
                                                  argumentsSerializer,
                                                  onErrorCallback);
                                }
                            }

                            var rule = field.Field.LogAccessRules.OrderByDescending(r => r.Severity).First();
                            SecurityLog.CreateRecord(
                                EnSecurityLogType.OperationGranted,
                                rule.Severity,
                                context,
                                rule.LogMessage,
                                idValue);
                        }
                    }

                    var resolvedProperty = propertyValue == null
                                               ? JValue.CreateNull()
                                               : await field.Resolver.ResolveQuery(
                                                     propertyValue,
                                                     fieldRequest,
                                                     field.Field,
                                                     context,
                                                     argumentsSerializer,
                                                     onErrorCallback);
                    result.Add(fieldName, resolvedProperty);
                }
                catch (Exception exception)
                {
                    onErrorCallback?.Invoke(exception);
                    result.Add(fieldName, JValue.CreateNull());
                }
            }

            return result;
        }

        /// <inheritdoc />
        internal override void CreateApiRoot(
            out List<ApiType> types,
            out JsonSerializer argumentsSerializer,
            out List<MutationDescription> mutationList,
            out List<string> errors)
        {
            this.CreateAllRelatedTypeListForApiRoot(out types, out argumentsSerializer, out errors);

            mutationList =
                this.GenerateMutations(
                    new List<string>(),
                    new List<string> { GeneratedType.TypeName },
                    (source, context, serializer, callback) => Task.FromResult(source)).ToList();
        }

        /// <inheritdoc />
        internal override IEnumerable<MutationDescription> GenerateMutations(
            List<string> path,
            List<string> typesUsed,
            GetMutationContainerDelegate getContainer)
        {
            foreach (var mutation in this.GenerateMutationsUntyped(path, getContainer))
            {
                yield return mutation;
            }

            foreach (var apiField in this.GenerateMutationsFromConnections(path, typesUsed, getContainer))
            {
                yield return apiField;
            }

            foreach (var mutation in this.GenerateMutationsFromFields(path, typesUsed, getContainer))
            {
                yield return mutation;
            }
        }

        /// <summary>
        /// Gets the list <see cref="ObjectResolver{T}"/> that a used to resolve the fields of current object
        /// </summary>
        /// <returns>The list of resolvers</returns>
        internal override IEnumerable<IResolver> GetDirectRelatedObjectResolvers()
        {
            return RelatedTypes.Where(t => t.Resolver != null).Select(t => t.Resolver);
        }

        /// <inheritdoc />
        internal override IEnumerable<ApiType> GetDirectRelatedVirtualTypes()
        {
            return RelatedTypes.Where(t => t.ApiType != null && !t.IsConnectionType).Select(t => t.ApiType);
        }

        /// <summary>
        /// Gets the names of type fields
        /// </summary>
        /// <returns>The names of type fields</returns>
        internal override Dictionary<MemberInfo, string> GetFieldsNames()
        {
            return Fields.ToDictionary(f => f.Value.TypeMember, f => f.Key);
        }

        /// <inheritdoc />
        internal override Task<Tuple<object, IResolver, ApiField>> GetNestedFieldValue(
            object source,
            Queue<string> fieldNames,
            RequestContext context,
            JsonSerializer argumentsSerializer,
            Action<Exception> onErrorCallback)
        {
            if (fieldNames.Count == 0)
            {
                return Task.FromResult<Tuple<object, IResolver, ApiField>>(null);
            }

            FieldDescription field;

            var fieldName = fieldNames.Dequeue();
            if (fieldName == null || !Fields.TryGetValue(fieldName, out field))
            {
                return Task.FromResult<Tuple<object, IResolver, ApiField>>(null);
            }

            var value = field.GetValue(
                (T)source,
                new ApiRequest { FieldName = fieldName },
                context,
                argumentsSerializer,
                onErrorCallback);

            if (fieldNames.Count == 0)
            {
                return
                    value.ContinueWith(
                        v => new Tuple<object, IResolver, ApiField>(v.Result, field.Resolver, field.Field));
            }

            var nestedResolver = field.Resolver as ObjectResolver;
            if (nestedResolver == null)
            {
                return Task.FromResult<Tuple<object, IResolver, ApiField>>(null);
            }

            return
                value.ContinueWith(
                    task =>
                        nestedResolver.GetNestedFieldValue(
                            task.Result,
                            fieldNames,
                            context,
                            argumentsSerializer,
                            onErrorCallback)).Unwrap();
        }

        /// <summary>
        /// Creates a resolver for the specified type
        /// </summary>
        /// <param name="fieldName">
        /// The field name.
        /// </param>
        /// <param name="field">
        /// The type field
        /// </param>
        /// <returns>
        /// The resolver
        /// </returns>
        private static IResolver CreateResolver(string fieldName, FieldDescription field)
        {
            var metadata = field.Metadata;

            var elementResolver = metadata.ScalarType == EnScalarType.DateTime 
                ? new DateTimeResolver() : metadata.ScalarType != EnScalarType.None
                                      ? typeof(ScalarResolver<>).MakeGenericType(metadata.Type)
                                          .CreateInstance<IResolver>()
                                      : metadata.Type.GetTypeInfo().IsSubclassOf(typeof(Enum))
                                          ? typeof(EnumResolver<>).MakeGenericType(metadata.Type)
                                              .CreateInstance<IResolver>()
                                          : typeof(ObjectResolver<>).MakeGenericType(metadata.Type)
                                              .CreateInstance<IResolver>();

            var elementObjectResolver = elementResolver as ObjectResolver;

            if (metadata.IsForwarding)
            {
                return new ForwarderResolver(elementResolver.GetElementType());
            }

            if (!metadata.GetFlags().HasFlag(EnFieldFlags.IsArray)
                && !metadata.GetFlags().HasFlag(EnFieldFlags.IsConnection))
            {
                return elementResolver;
            }

            if (elementObjectResolver != null)
            {
                var objectKeys =
                    elementObjectResolver.GetApiType().Fields.Count(f => f.Flags.HasFlag(EnFieldFlags.IsKey));
                if (objectKeys == 0)
                {
                    GenerationErrors.Add($"Field {fieldName} is an array of {metadata.TypeName} that has no key fields");
                }
                else if (objectKeys > 1)
                {
                    GenerationErrors.Add(
                        $"Field {fieldName} is an array of {metadata.TypeName} that has multiple key fields");
                }
                else
                {
                    var collectionType = typeof(CollectionResolver<>).MakeGenericType(metadata.Type);

                    var filterType =
                        (ApiObjectType)
                        collectionType.GetProperty("FilterType", BindingFlags.Static | BindingFlags.Public)
                            ?.GetValue(null);

                    if (filterType == null)
                    {
                        throw new InvalidOperationException("Unexpected CollectionResolver signature change");
                    }

                    if (filterType.Fields.Count > 2 && RelatedTypes.All(rt => rt.ApiType != filterType))
                    {
                        RelatedTypes.Add(new RelatedType { ApiType = filterType });
                    }

                    var sortType =
                        (ApiEnumType)
                        collectionType.GetProperty("SortType", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);

                    if (sortType == null)
                    {
                        throw new InvalidOperationException("Unexpected CollectionResolver signature change");
                    }

                    if (sortType.Values.Any() && RelatedTypes.All(rt => rt.ApiType != sortType))
                    {
                        RelatedTypes.Add(new RelatedType { ApiType = sortType });
                    }

                    if (metadata.GetFlags().HasFlag(EnFieldFlags.IsConnection))
                    {
                        var connectionResolverType = typeof(ConnectionResolver<>).MakeGenericType(metadata.Type);
                        return connectionResolverType.CreateInstance<IResolver>();
                    }

                    field.Field.Flags &= ~EnFieldFlags.IsArray;
                    field.Field.Flags |= EnFieldFlags.IsConnection;
                    return collectionType.CreateInstance<IResolver>();
                }
            }
            else if (metadata.GetFlags().HasFlag(EnFieldFlags.IsArray))
            {
                return new SimpleCollectionResolver(elementResolver);
            }

            return new NullResolver();
        }

        /// <summary>
        /// Generates api field from type method
        /// </summary>
        /// <param name="method">
        /// The method to process
        /// </param>
        /// <param name="attribute">
        /// The method description attribute
        /// </param>
        /// <returns>
        /// The field description
        /// </returns>
        private static FieldDescription GenerateFieldFromMethod(MethodInfo method, PublishToApiAttribute attribute)
        {
            var type = method.DeclaringType;
            TypeMetadata metadata;
            try
            {
                metadata = TypeMetadata.GenerateTypeMetadata(method.ReturnType, attribute);
            }
            catch (Exception exception)
            {
                GenerationErrors.Add(
                    $"Error while parsing return type of method {method.Name} of type {type?.FullName}: {exception.Message}");
                return null;
            }

            var name = attribute.GetName(method);

            ApiField field;
            if (metadata.ScalarType != EnScalarType.None)
            {
                field = ApiField.Scalar(
                    name,
                    metadata.ScalarType,
                    metadata.GetFlags(),
                    description: attribute.Description);
            }
            else
            {
                field = ApiField.Object(
                    name,
                    ApiDescriptionAttribute.GetTypeName(metadata.Type),
                    metadata.GetFlags(),
                    description: attribute.Description);

                if (RelatedTypes.All(rt => rt.Type != metadata.Type))
                {
                    RelatedTypes.Add(new RelatedType { Type = metadata.Type });
                }
            }

            foreach (var parameterInfo in method.GetParameters())
            {
                if (parameterInfo.ParameterType == typeof(RequestContext))
                {
                    continue;
                }

                if (parameterInfo.ParameterType == typeof(ApiRequest))
                {
                    continue;
                }

                var parameterMetadata = TypeMetadata.GenerateTypeMetadata(
                    parameterInfo.ParameterType,
                    new DeclareFieldAttribute());
                var description =
                    parameterInfo.GetCustomAttribute(typeof(ApiDescriptionAttribute)) as ApiDescriptionAttribute;

                var parameterName = ApiDescriptionAttribute.GetParameterName(parameterInfo);
                if (parameterMetadata.ScalarType != EnScalarType.None)
                {
                    field.Arguments.Add(
                        ApiField.Scalar(
                            parameterName,
                            parameterMetadata.ScalarType,
                            parameterMetadata.GetFlags(),
                            description: description?.Description));
                }
                else
                {
                    field.Arguments.Add(
                        ApiField.Object(
                            parameterName,
                            ApiDescriptionAttribute.GetTypeName(parameterInfo.ParameterType),
                            parameterMetadata.GetFlags(),
                            description: description?.Description));

                    if (RelatedTypes.All(rt => rt.Type != parameterMetadata.Type))
                    {
                        RelatedTypes.Add(new RelatedType { Type = parameterMetadata.Type });
                    }

                    if (metadata.GetFlags().HasFlag(EnFieldFlags.IsConnection))
                    {
                        var mutationResult = typeof(MutationResult<>).MakeGenericType(metadata.Type);
                        if (RelatedTypes.All(rt => rt.Type != mutationResult))
                        {
                            RelatedTypes.Add(new RelatedType { Type = mutationResult });
                        }
                    }
                }
            }

            field.FillAuthorizationProperties(method);
            return new FieldDescription(field, metadata, method, attribute is DeclareMutationAttribute);
        }

        /// <summary>
        /// Generates api field from type method
        /// </summary>
        /// <param name="property">
        /// The property to process
        /// </param>
        /// <param name="attribute">
        /// The method description attribute
        /// </param>
        /// <returns>
        /// The field description
        /// </returns>
        private static FieldDescription GenerateFieldFromProperty(
            PropertyInfo property,
            PublishToApiAttribute attribute)
        {
            var declaringType = property.DeclaringType;

            TypeMetadata metadata;
            try
            {
                metadata = TypeMetadata.GenerateTypeMetadata(property.PropertyType, attribute);
            }
            catch (Exception exception)
            {
                GenerationErrors.Add(
                    $"Error while parsing return type of property {property.Name} of type {declaringType?.FullName}: {exception.Message}");
                return null;
            }

            var name = attribute.GetName(property);

            var flags = metadata.GetFlags();

            if ((metadata.Type.GetTypeInfo().IsSubclassOf(typeof(Enum)) || metadata.ScalarType != EnScalarType.None)
                && !metadata.IsAsync && !metadata.IsForwarding
                && (metadata.MetaType == TypeMetadata.EnMetaType.Object
                    || metadata.MetaType == TypeMetadata.EnMetaType.Scalar))
            {
                flags |= EnFieldFlags.IsFilterable | EnFieldFlags.IsSortable;
            }

            if (!metadata.IsAsync && !metadata.IsForwarding && metadata.ConverterType == null && property.CanWrite
                && metadata.MetaType != TypeMetadata.EnMetaType.Connection
                && (!(attribute is DeclareFieldAttribute)
                    || ((DeclareFieldAttribute)attribute).Access.HasFlag(EnAccessFlag.Writable)))
            {
                flags |= EnFieldFlags.CanBeUsedInInput;
            }

            if (attribute is DeclareFieldAttribute
                && !((DeclareFieldAttribute)attribute).Access.HasFlag(EnAccessFlag.Queryable))
            {
                flags &= ~EnFieldFlags.Queryable;
            }

            if (metadata.ScalarType != EnScalarType.None)
            {
                if ((attribute as DeclareFieldAttribute)?.IsKey == true)
                {
                    flags |= EnFieldFlags.IsKey;
                }

                var scalar = ApiField.Scalar(name, metadata.ScalarType, flags, description: attribute.Description);
                scalar.FillAuthorizationProperties(property);
                return new FieldDescription(scalar, metadata, property, attribute is DeclareMutationAttribute);
            }

            var field = ApiField.Object(
                name,
                ApiDescriptionAttribute.GetTypeName(metadata.Type),
                flags,
                description: attribute.Description);

            if (RelatedTypes.All(rt => rt.Type != metadata.Type))
            {
                RelatedTypes.Add(new RelatedType { Type = metadata.Type });
            }

            if (metadata.GetFlags().HasFlag(EnFieldFlags.IsConnection))
            {
                var mutationResult = typeof(MutationResult<>).MakeGenericType(metadata.Type);
                if (RelatedTypes.All(rt => rt.Type != mutationResult))
                {
                    RelatedTypes.Add(new RelatedType { Type = mutationResult });
                }

                if (metadata.RealConnectionType != null
                    && RelatedTypes.All(rt => rt.Type != metadata.RealConnectionType))
                {
                    RelatedTypes.Add(new RelatedType { Type = metadata.RealConnectionType, IsConnectionType = true });
                }
            }

            field.FillAuthorizationProperties(property);
            return new FieldDescription(field, metadata, property, attribute is DeclareMutationAttribute);
        }

        /// <summary>
        /// Generate fields from type methods
        /// </summary>
        /// <returns>The field api description</returns>
        private static IEnumerable<FieldDescription> GenerateTypeMethods()
        {
            var type = typeof(T);
            return
                type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Select(p => new { Method = p, Attribute = p.GetCustomAttribute<PublishToApiAttribute>() })
                    .Where(p => p.Attribute != null && !p.Method.IsGenericMethod && !p.Method.IsGenericMethodDefinition)
                    .Select(m => GenerateFieldFromMethod(m.Method, m.Attribute))
                    .Where(f => f != null);
        }

        /// <summary>
        /// Generate fields from type properties
        /// </summary>
        /// <returns>The field api description</returns>
        private static IEnumerable<FieldDescription> GenerateTypeProperties()
        {
            var type = typeof(T);
            return
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(p => new { Property = p, Attribute = p.GetCustomAttribute<PublishToApiAttribute>() })
                    .Where(p => p.Attribute != null && p.Property.CanRead)
                    .Select(o => GenerateFieldFromProperty(o.Property, o.Attribute))
                    .Where(f => f != null);
        }

        /// <summary>
        /// Generates a value getter for the specified property
        /// </summary>
        /// <param name="fieldDescription">The field description</param>
        /// <returns>The value getter function</returns>
        private static PropertyValueGetter GenerateValueGetter(FieldDescription fieldDescription)
        {
            var sourceParam = Expression.Parameter(typeof(T), "source");
            var requestParam = Expression.Parameter(typeof(ApiRequest), "request");
            var contextParam = Expression.Parameter(typeof(RequestContext), "context");
            var serializerParam = Expression.Parameter(typeof(JsonSerializer), "serializer");
            var callbackParam = Expression.Parameter(typeof(Action<Exception>), "errorCallback");

            var methodInfo = fieldDescription.TypeMember as MethodInfo;
            var propertyInfo = fieldDescription.TypeMember as PropertyInfo;

            if (methodInfo == null && propertyInfo == null)
            {
                GenerationErrors.Add($"Field {fieldDescription.Field.Name} is neither property nor method");
                return (source, request, context, serializer, callback) => Task.FromResult<object>(null);
            }

            Expression valueGetter;
            Type valueType;
            if (methodInfo != null)
            {
                valueType = methodInfo.ReturnType;
                var methodArguments = new List<Expression>();

                var jsonPropertyMethod = typeof(JObject).GetMethod(nameof(JObject.Property), new[] { typeof(string) });
                var jsonPropertyValue = typeof(JProperty).GetProperty(nameof(JProperty.Value));
                var jsonTokenToObject = typeof(JToken).GetMethod(nameof(JProperty.ToObject), new[] { typeof(JsonSerializer) });

                if (jsonPropertyMethod == null || jsonPropertyValue == null || jsonTokenToObject == null)
                {
                    throw new InvalidOperationException("Unexpected Json library signature change");
                }

                var argumentsProperty = typeof(ApiRequest).GetProperty(nameof(ApiRequest.Arguments));
                if (argumentsProperty == null)
                {
                    throw new InvalidOperationException("Unexpected ApiRequest signature change");
                }

                var arguments =
                    Expression.Convert(
                        Expression.Property(requestParam, argumentsProperty),
                        typeof(JObject));

                foreach (var parameterInfo in methodInfo.GetParameters())
                {
                    var parameterName = ApiDescriptionAttribute.GetParameterName(parameterInfo);
                    if (parameterInfo.ParameterType == typeof(RequestContext))
                    {
                        methodArguments.Add(contextParam);
                    }
                    else if (parameterInfo.ParameterType == typeof(ApiRequest))
                    {
                        methodArguments.Add(requestParam);
                    }
                    else
                    {
                        var jsonProperty =
                            Expression.Condition(
                                Expression.NotEqual(arguments, Expression.Constant(null)),
                                Expression.Call(arguments, jsonPropertyMethod, Expression.Constant(parameterName)),
                                Expression.Constant(null, typeof(JProperty)));

                        var jsonValue =
                            Expression.Condition(
                                Expression.NotEqual(jsonProperty, Expression.Constant(null)),
                                Expression.Property(jsonProperty, jsonPropertyValue),
                                Expression.Constant(null, typeof(JToken)));

                        var valueDeserialization = Expression.Call(
                            jsonValue,
                            jsonTokenToObject.MakeGenericMethod(parameterInfo.ParameterType),
                            serializerParam);

                        var argument = Expression.Condition(
                            Expression.NotEqual(jsonValue, Expression.Constant(null)),
                            valueDeserialization,
                            Expression.Default(parameterInfo.ParameterType));

                        methodArguments.Add(argument);
                    }
                }

                valueGetter = Expression.Call(sourceParam, methodInfo, methodArguments);
            }
            else
            {
                valueType = propertyInfo.PropertyType;
                valueGetter = Expression.Property(sourceParam, propertyInfo);
            }

            Func<Expression, Expression> converterExpression = null;
            if (fieldDescription.Metadata.ConverterType != null)
            {
                var constructor = fieldDescription.Metadata.ConverterType.GetConstructor(new Type[0]);
                if (constructor != null)
                {
                    var converterInstance = Expression.New(constructor);
                    var converterMethod = fieldDescription.Metadata.ConverterType.GetMethod(
                        "Convert",
                        new[] { typeof(object) });

                    if (converterMethod != null)
                    {
                        converterExpression = param => Expression.Call(converterInstance, converterMethod, param);
                    }
                }
            }

            if (!fieldDescription.Metadata.IsAsync)
            {
                var taskCreator = typeof(Task).GetMethod("FromResult").MakeGenericMethod(typeof(object));
                if (converterExpression != null)
                {
                    valueGetter = converterExpression(valueGetter);
                }

                valueGetter = Expression.Call(taskCreator, Expression.Convert(valueGetter, typeof(object)));
            }
            else
            {
                valueGetter = GenerateValueGetterConvertResultToObject(valueType, valueGetter, converterExpression);
            }

            var function = Expression.Lambda<PropertyValueGetter>(
                valueGetter,
                sourceParam,
                requestParam,
                contextParam,
                serializerParam,
                callbackParam);

            return function.Compile();
        }

        /// <summary>
        /// Creates an expression that will convert result value to object
        /// </summary>
        /// <param name="returnType">
        /// The return object type
        /// </param>
        /// <param name="getValue">
        /// The value getter expression
        /// </param>
        /// <param name="converterExpression">
        /// Function to convert actual value with use of converter
        /// </param>
        /// <returns>
        /// The conversed type expression
        /// </returns>
        private static Expression GenerateValueGetterConvertResultToObject(
            Type returnType,
            Expression getValue,
            Func<Expression, Expression> converterExpression)
        {
            var asyncType = ApiDescriptionAttribute.CheckType(returnType, typeof(Task<>));
            if (asyncType != null)
            {
                returnType = asyncType.GenericTypeArguments[0];
            }

            var taskType = typeof(Task<>).MakeGenericType(returnType);

            var taskResult = taskType.GetProperty("Result");
            if (taskResult == null)
            {
                throw new InvalidOperationException("Unexpected task signature changed");
            }

            var continueMethod =
                taskType.GetMethods()
                    .First(
                        m =>
                            m.Name == "ContinueWith" && m.IsGenericMethod && m.GetParameters().Length == 1
                            && m.GetParameters().First().ParameterType.GetGenericTypeDefinition() == typeof(Func<,>)
                            && m.GetParameters().First().ParameterType.GenericTypeArguments[0] == taskType)
                    .MakeGenericMethod(typeof(object));

            var resultParameter = Expression.Parameter(taskType, "task");

            Expression result = Expression.Property(resultParameter, taskResult);
            if (converterExpression != null)
            {
                result = converterExpression(result);
            }

            var conversion = Expression.Lambda(Expression.Convert(result, typeof(object)), resultParameter);
            getValue = Expression.Call(getValue, continueMethod, conversion);

            return getValue;
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
                foreach (var field in Fields.Values)
                {
                    field.Resolver = CreateResolver(field.Field.Name, field);
                    foreach (var typeArgument in field.Resolver.GetTypeArguments())
                    {
                        var argument = typeArgument.Clone();
                        argument.Flags |= EnFieldFlags.IsTypeArgument;
                        field.Field.Arguments.Add(argument);
                    }

                    field.GetValue = GenerateValueGetter(field);
                }

                foreach (var relatedType in RelatedTypes.Where(r => r.Type != null))
                {
                    relatedType.Resolver = relatedType.Type.GetTypeInfo().IsSubclassOf(typeof(Enum))
                                               ? typeof(EnumResolver<>).MakeGenericType(relatedType.Type)
                                                   .CreateInstance<IResolver>()
                                               : typeof(ObjectResolver<>).MakeGenericType(relatedType.Type)
                                                   .CreateInstance<IResolver>();
                    relatedType.ApiType = relatedType.Resolver.GetElementType();
                }
            }
        }

        /// <summary>
        /// Creates an api root data from current type
        /// </summary>
        /// <param name="types">
        /// The list of all types used in api
        /// </param>
        /// <param name="argumentsSerializer">
        /// The configured JSON serializer to deserialize method arguments
        /// </param>
        /// <param name="errors">
        /// The list of generation errors.
        /// </param>
        private void CreateAllRelatedTypeListForApiRoot(
            out List<ApiType> types,
            out JsonSerializer argumentsSerializer,
            out List<string> errors)
        {
            var directTypes = this.GetDirectRelatedObjectResolvers().ToList();
            directTypes.Add(this);

            types = new List<ApiType> { GeneratedType };
            var resolveQueue = new Queue<IResolver>(directTypes);
            var resolvedTypes = new Dictionary<string, IResolver>();
            var memberNames = new Dictionary<MemberInfo, string>();

            errors = new List<string>();
            errors.AddRange(this.Errors);

            while (resolveQueue.Count > 0)
            {
                var resolver = resolveQueue.Dequeue();
                var apiType = resolver.GetElementType();
                if (resolvedTypes.ContainsKey(apiType.TypeName))
                {
                    continue;
                }

                resolvedTypes[apiType.TypeName] = resolver;
                
                var genericResolver = resolver as ObjectResolver;
                if (genericResolver == null)
                {
                    continue;
                }

                errors.AddRange(genericResolver.Errors.Select(e => $"{apiType}: {e}"));

                foreach (var type in genericResolver.GetDirectRelatedVirtualTypes())
                {
                    if (!types.Contains(type))
                    {
                        if ((type as ApiObjectType)?.Fields.Count == 0)
                        {
                            errors.Add($"{apiType}: Object has no fields");
                        }

                        types.Add(type);
                    }
                }

                foreach (var pair in genericResolver.GetFieldsNames())
                {
                    memberNames[pair.Key] = pair.Value;
                }

                foreach (var objectResolver in genericResolver.GetDirectRelatedObjectResolvers())
                {
                    resolveQueue.Enqueue(objectResolver);
                }
            }

            var jsonContractResolver = new InputContractResolver(memberNames);
            argumentsSerializer = new JsonSerializer { ContractResolver = jsonContractResolver };
        }

        /// <summary>
        /// Generate mutations directly declared in the type
        /// </summary>
        /// <param name="path">
        /// The fields path
        /// </param>
        /// <param name="typesUsed">Already used types to avoid circular references</param>
        /// <param name="getContainer">
        /// The container resolver method
        /// </param>
        /// <returns>
        /// The list of mutations
        /// </returns>
        private IEnumerable<MutationDescription> GenerateMutationsFromConnections(
            List<string> path,
            List<string> typesUsed,
            GetMutationContainerDelegate getContainer)
        {
            var connections =
                Fields.Values.Where(
                    f =>
                        f.Field.Flags.HasFlag(EnFieldFlags.IsConnection) && f.Field.ScalarType == EnScalarType.None
                        && f.Field.Arguments.All(a => a.Flags.HasFlag(EnFieldFlags.IsTypeArgument)));

            foreach (var connection in connections)
            {
                var attribute = connection.TypeMember.GetCustomAttribute<DeclareConnectionAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                if (connection.Metadata.KeyProperty == null)
                {
                    GenerationErrors.Add(
                        $"Connection {connection.Field.Name} of type {NamingUtilities.ToCSharpRepresentation(typeof(T))} is defined for intities without key field");
                    continue;
                }

                var idScalarType = TypeMetadata.CheckScalarType(connection.Metadata.KeyProperty.PropertyType);
                if (idScalarType == EnScalarType.None)
                {
                    GenerationErrors.Add(
                        $"Defined type of id should be scalar type on connection {connection.Field.Name} of type {NamingUtilities.ToCSharpRepresentation(typeof(T))}");
                    continue;
                }

                var mutationResultType = typeof(MutationResult<>).MakeGenericType(connection.Metadata.Type);

                var mutationResult =
                    typeof(ObjectResolver<>).MakeGenericType(mutationResultType)
                        .CreateInstance<ObjectResolver>()
                        .GetApiType();

                var nextPath = new List<string>(path) { connection.Field.Name };
                var requestPath =
                    nextPath.Select(p => new ApiRequest { FieldName = p })
                        .ToList();

                GetMutationContainerDelegate nextResolver =
                    (source, context, serializer, callback) =>
                        getContainer(source, context, serializer, callback)
                            .ContinueWith(
                                result =>
                                    this.GetFieldValue(result.Result, connection.Field.Name, context, serializer, callback))
                            .Unwrap();

                var connectionType = (connection.TypeMember as MethodInfo)?.ReturnType
                                     ?? (connection.TypeMember as PropertyInfo)?.PropertyType;
                if (connectionType == null)
                {
                    continue;
                }

                if (connection.Metadata.RealConnectionType != null)
                {
                    var resolver =
                        typeof(ObjectResolver<>).MakeGenericType(connection.Metadata.RealConnectionType)
                            .CreateInstance<ObjectResolver>();

                    var additionalMutations = resolver.GenerateMutations(nextPath, typesUsed, nextResolver);
                    foreach (var mutation in additionalMutations)
                    {
                        if (mutation.Field.TypeName == mutationResult.TypeName)
                        {
                            mutation.Type = ApiMutation.EnType.Connection;
                        }

                        yield return mutation;
                    }
                }

                if (attribute.CanCreate)
                {
                    var field = ApiField.Object(
                        "create",
                        mutationResult.TypeName,
                        EnFieldFlags.Queryable | EnFieldFlags.IsConnection,
                        new List<ApiField>
                            {
                                ApiField.Object(
                                    "newNode",
                                    connection.Field.TypeName,
                                    description: "The object's data")
                            },
                        attribute.CreateDescription);
                    field.FillAuthorizationProperties(connection.TypeMember);
                    yield return
                        new MutationDescription
                            {
                                Field = field,
                                Path = requestPath,
                                Type = ApiMutation.EnType.ConnectionCreate,
                                Resolver = connection.Resolver,
                                ResolveContainer = nextResolver
                            };
                }

                if (attribute.CanUpdate)
                {
                    var field = ApiField.Object(
                        "update",
                        mutationResult.TypeName,
                        EnFieldFlags.Queryable | EnFieldFlags.IsConnection,
                        new List<ApiField>
                            {
                                ApiField.Scalar("id", idScalarType, description: "The object's id"),
                                ApiField.Object(
                                    "newNode",
                                    connection.Field.TypeName,
                                    description: "The object's data")
                            },
                        attribute.CreateDescription);
                    field.FillAuthorizationProperties(connection.TypeMember);
                    yield return
                        new MutationDescription
                            {
                                Field = field,
                                Path = requestPath,
                                Type = ApiMutation.EnType.ConnectionUpdate,
                                Resolver = connection.Resolver,
                                ResolveContainer = nextResolver
                            };
                }

                if (attribute.CanDelete)
                {
                    var field = ApiField.Object(
                        "delete",
                        mutationResult.TypeName,
                        EnFieldFlags.Queryable | EnFieldFlags.IsConnection,
                        new List<ApiField> { ApiField.Scalar("id", idScalarType, description: "The object's id") },
                        attribute.CreateDescription);
                    field.FillAuthorizationProperties(connection.TypeMember);
                    yield return
                        new MutationDescription
                            {
                                Field = field,
                                Path = requestPath,
                                Type = ApiMutation.EnType.ConnectionDelete,
                                Resolver = connection.Resolver,
                                ResolveContainer = nextResolver
                            };
                }
            }
        }

        /// <summary>
        /// Generate mutation from type fields
        /// </summary>
        /// <param name="path">The type field path</param>
        /// <param name="typesUsed">Already used types to avoid circular references</param>
        /// <param name="getContainer">The container getter</param>
        /// <returns>The list of mutations</returns>
        private IEnumerable<MutationDescription> GenerateMutationsFromFields(
            List<string> path,
            List<string> typesUsed,
            GetMutationContainerDelegate getContainer)
        {
            var possibleMutationSubcontainers =
                Fields.Values.Where(
                    f =>
                        !f.Field.Arguments.Any() && f.Field.ScalarType == EnScalarType.None
                        && !f.Field.Flags.HasFlag(EnFieldFlags.IsArray)
                        && !f.Field.Flags.HasFlag(EnFieldFlags.IsConnection));

            foreach (var subContainer in possibleMutationSubcontainers)
            {
                if (typesUsed.Contains(subContainer.Field.TypeName))
                {
                    // circular type use
                    continue;
                }

                var subTypeResolver = subContainer.Resolver as ObjectResolver;
                if (subTypeResolver == null)
                {
                    continue;
                }

                var newPath = new List<string>(path) { subContainer.Field.Name };
                var newTypesUsed = new List<string>(typesUsed) { subContainer.Field.TypeName };
                GetMutationContainerDelegate nextResolver =
                    (source, context, serializer, callback) =>
                        getContainer(source, context, serializer, callback)
                            .ContinueWith(
                                result =>
                                    this.GetFieldValue(result.Result, subContainer.Field.Name, context, serializer, callback))
                            .Unwrap();

                foreach (var generateMutation in subTypeResolver.GenerateMutations(newPath, newTypesUsed, nextResolver))
                {
                    yield return generateMutation;
                }
            }
        }

        /// <summary>
        /// Gets the untyped mutation declared in type
        /// </summary>
        /// <param name="path">The path to get to this type</param>
        /// <param name="getContainer">The container getter</param>
        /// <returns>The list of untyped mutations</returns>
        private IEnumerable<MutationDescription> GenerateMutationsUntyped(
            List<string> path,
            GetMutationContainerDelegate getContainer)
        {
            foreach (var field in Fields.Values.Where(f => f.IsMutation))
            {
                yield return
                    new MutationDescription
                        {
                            Field = field.Field,
                            Path = path.Select(p => new ApiRequest { FieldName = p }).ToList(),
                            Type = ApiMutation.EnType.Untyped,
                            ResolveContainer = getContainer,
                            Resolver = this
                        };
            }
        }

        /// <summary>
        /// Gets a value of specified field without arguments
        /// </summary>
        /// <param name="source">
        /// The source object
        /// </param>
        /// <param name="fieldName">
        /// The field n
        /// </param>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="argumentsSerializer">
        /// The arguments serializer.
        /// </param>
        /// <param name="onErrorCallback">
        /// The on error callback.
        /// </param>
        /// <returns>The value</returns>
        private Task<object> GetFieldValue(
            object source,
            string fieldName,
            RequestContext context,
            JsonSerializer argumentsSerializer,
            Action<Exception> onErrorCallback)
        {
            FieldDescription field;
            if (!Fields.TryGetValue(fieldName, out field))
            {
                return Task.FromResult<object>(null);
            }

            if (source != null && !(source is T))
            {
                onErrorCallback(new Exception("ObjectResolver GetFieldValue error. "
                                              + $"Expected source of type {NamingUtilities.ToCSharpRepresentation(typeof(T))} "
                                              + $"but received {NamingUtilities.ToCSharpRepresentation(source.GetType())}"));
                return Task.FromResult<object>(null);
            }

            return field.GetValue(
                (T)source,
                new ApiRequest { FieldName = fieldName },
                context,
                argumentsSerializer,
                onErrorCallback);
        }

        /// <summary>
        /// The type field description
        /// </summary>
        private class FieldDescription
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="FieldDescription"/> class.
            /// </summary>
            /// <param name="field">
            /// The field.
            /// </param>
            /// <param name="metadata">
            /// The metadata.
            /// </param>
            /// <param name="typeMember">
            /// The type member.
            /// </param>
            /// <param name="isMutation">
            /// A value indicating whether this field is mutation
            /// </param>
            public FieldDescription(ApiField field, TypeMetadata metadata, MemberInfo typeMember, bool isMutation)
            {
                this.Field = field;
                this.Metadata = metadata;
                this.TypeMember = typeMember;
                this.IsMutation = isMutation;
            }

            /// <summary>
            /// Gets the api field
            /// </summary>
            public ApiField Field { get; }

            /// <summary>
            /// Gets or sets a field value resolver method
            /// </summary>
            public PropertyValueGetter GetValue { get; set; }

            /// <summary>
            /// Gets a value indicating whether this field is mutation
            /// </summary>
            public bool IsMutation { get; }

            /// <summary>
            /// Gets the field metadata
            /// </summary>
            public TypeMetadata Metadata { get; }

            /// <summary>
            /// Gets or sets the resolver 
            /// </summary>
            public IResolver Resolver { get; set; }

            /// <summary>
            /// Gets the type member
            /// </summary>
            public MemberInfo TypeMember { get; }
        }
    }
}