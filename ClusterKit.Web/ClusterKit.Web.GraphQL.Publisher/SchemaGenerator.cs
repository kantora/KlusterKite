﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SchemaGenerator.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   Generator of the GraphQL scheme from api providers
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.Web.GraphQL.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Akka.Util.Internal;

    using ClusterKit.API.Client;
    using ClusterKit.Security.Client;
    using ClusterKit.Web.GraphQL.Publisher.GraphTypes;
    using ClusterKit.Web.GraphQL.Publisher.Internals;

    using global::GraphQL;
    using global::GraphQL.Http;
    using global::GraphQL.Types;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Generator of the GraphQL scheme from api providers
    /// </summary>
    public static class SchemaGenerator
    {
        /// <summary>
        /// Generates GraphQL schema
        /// </summary>
        /// <param name="providers">The list of providers</param>
        /// <returns>The new GraphQL schema</returns>
        public static Schema Generate(List<ApiProvider> providers)
        {
            var createdTypes = new Dictionary<string, MergedType>();
            var api = MergeApis(providers, createdTypes);
            api.Initialize();
            var nodeInterface = new NodeInterface();
            var root = new MergedRoot("Query", providers, api);
            root.Initialize();
            createdTypes[root.ComplexTypeName] = root;
            createdTypes[api.ComplexTypeName] = api;

            var types = createdTypes.Values.ToList();

            var typeNames = types.Select(t => t.ComplexTypeName).Distinct().ToList();

            var graphTypes = typeNames.ToDictionary(
                typeName => typeName,
                typeName => types.FirstOrDefault(t => t.ComplexTypeName == typeName)?.GenerateGraphType(nodeInterface));

            var mutationType = api.GenerateMutationType();
            graphTypes[mutationType.Name] = mutationType;
            graphTypes.Values.OfType<IComplexGraphType>().SelectMany(a => a.Fields).ForEach(
                f =>
                    {
                        var fieldDescription = f.GetMetadata<MergedField>(MergedType.MetaDataTypeKey);
                        if (fieldDescription == null)
                        {
                            return;
                        }

                        var typeArguments = fieldDescription.Type.GenerateArguments(graphTypes) ?? new QueryArguments();
                        var fieldArguments =
                            fieldDescription.Arguments.Select(
                                p =>
                                    new QueryArgument(typeof(VirtualInputGraphType))
                                        {
                                            Name = p.Key,
                                            ResolvedType =
                                                p.Value.Flags.HasFlag(
                                                    EnFieldFlags.IsArray)
                                                    ? new ListGraphType(
                                                        graphTypes[p.Value.Type.ComplexTypeName])
                                                    : graphTypes[p.Value.Type.ComplexTypeName],
                                            Description =
                                                p.Value.Description
                                        });

                        var resultingArguments = typeArguments.Union(fieldArguments).ToList();

                        if (resultingArguments.Any())
                        {
                            f.Arguments = new QueryArguments(resultingArguments);
                        }

                        f.ResolvedType = fieldDescription.Flags.HasFlag(EnFieldFlags.IsArray)
                                             ? new ListGraphType(graphTypes[fieldDescription.Type.ComplexTypeName])
                                             : graphTypes[fieldDescription.Type.ComplexTypeName];

                        if (f.Resolver == null)
                        {
                            f.Resolver = fieldDescription.Resolver ?? fieldDescription.Type;
                        }

                        if (!string.IsNullOrWhiteSpace(fieldDescription.Description))
                        {
                            f.Description = fieldDescription.Description;
                        }
                    });

            graphTypes.Values.OfType<VirtualGraphType>().ForEach(vgt => vgt.StoreFieldResolvers());

            var schema = new Schema
                             {
                                 Query = (VirtualGraphType)graphTypes[root.ComplexTypeName],
                                 Mutation = mutationType.Fields.Any() ? mutationType : null
                             };

            schema.Initialize();
            return schema;
        }

        /// <summary>
        /// Check the generated schema for possible errors
        /// </summary>
        /// <param name="schema">The generated schema</param>
        /// <returns>The list of errors</returns>
        public static IEnumerable<string> CheckSchema(Schema schema)
        {
            var types = schema.AllTypes.ToDictionary(t => t.Name);
            var checkedInputGraphTypes = new List<string>();
            foreach (
                var graphType in
                types.Values.OfType<IComplexGraphType>()
                    .Where(g => !g.GetType().FullName.StartsWith("GraphQL.Introspection")))
            {
                foreach (var field in graphType.Fields)
                {
                    var resolvedType = GetResolvedType(field.ResolvedType);
                    if (resolvedType?.Name == null || !types.ContainsKey(resolvedType.Name))
                    {
                        yield return $"Field {field.Name} of type {graphType.Name} has unregistered type";
                    }

                    if (field.Arguments == null)
                    {
                        continue;
                    }

                    foreach (var argument in field.Arguments)
                    {
                        resolvedType = GetResolvedType(argument.ResolvedType);
                        IGraphType argumentType;
                        if (resolvedType?.Name == null
                            || !types.TryGetValue(resolvedType.Name, out argumentType))
                        {
                            yield return
                                $"Field {field.Name} of type {graphType.Name} has argument {argument.Name} unregistered type";
                            continue;
                        }

                        if (!(argumentType is ScalarGraphType) && !(argumentType is IInputGraphType) && !(argumentType is InputObjectGraphType))
                        {
                            yield return
                                $"Field {field.Name} of type {graphType.Name} has argument {argument.Name} has invalid type {argumentType.Name}";
                            continue;
                        }

                        var complexGraphType = argumentType as IComplexGraphType;
                        if (complexGraphType == null)
                        {
                            continue;
                        }

                        foreach (var error in CheckInputGraphType(complexGraphType, checkedInputGraphTypes))
                        {
                            yield return error;
                        }
                    }
                }

                if (!graphType.Fields.Any())
                {
                    yield return $"Type {graphType.Name} has no fields";
                }
            }
        }

        /// <summary>
        /// Check the schema against introspection query
        /// </summary>
        /// <param name="schema">The schema</param>
        /// <returns>List of errors</returns>
        public static IEnumerable<string> CheckSchemaIntrospection(Schema schema)
        {
            var result = new DocumentExecuter().ExecuteAsync(
                                         r =>
                                         {
                                             r.Schema = schema;
                                             r.Query = Queries.IntrospectionQuery;
                                             r.UserContext = new RequestContext();
                                         }).Result;
            var response = new DocumentWriter(true).Write(result);
            var json = JObject.Parse(response);
            var types = (json.SelectToken("data.__schema.types") as JArray)?.ToDictionary(p => ((JObject)p).Property("name")?.Value, p => (JObject)p);
            if (types == null)
            {
                yield return "Could not get types list via introspection";
                yield break;
            }

            var inputTypesChecked = new List<string>();
            foreach (var type in types.Values.Where(t => !t.Property("name").Value.ToObject<string>().StartsWith("__")))
            {
                var typeName = type.Property("name").Value.ToObject<string>();
                var fields = type.Property("fields")?.Value as JArray;
                if (fields != null)
                {
                    foreach (var field in fields)
                    {
                        var fieldType = field.SelectToken("type.kind")?.ToObject<string>() == "LIST"
                            ? field.SelectToken("type.ofType.name")?.ToObject<string>()
                            : field.SelectToken("type.name")?.ToObject<string>();
                        var fieldName = field.SelectToken("name")?.ToObject<string>();
                        if (!types.ContainsKey(fieldType ?? string.Empty))
                        {
                            yield return $"{typeName} property {fieldName} has unknown type {fieldType}";
                        }

                        var arguments = field.SelectToken("args") as JArray;
                        if (arguments == null || arguments.Count == 0)
                        {
                            continue;
                        }

                        foreach (var argument in arguments)
                        {
                            var argumentType = argument.SelectToken("type.kind")?.ToObject<string>() == "LIST"
                                                   ? argument.SelectToken("type.ofType.name")?.ToObject<string>()
                                                   : argument.SelectToken("type.name")?.ToObject<string>();
                            var argumentName = argument.SelectToken("name")?.ToObject<string>();

                            JObject argumentTypeJson;
                            if (!types.TryGetValue(argumentType ?? string.Empty, out argumentTypeJson))
                            {
                                yield return
                                    $"{typeName} property {fieldName} has argument {argumentName} of unknown type {argumentType}";
                            }
                            else
                            {
                                var argumentTypeKind = argumentTypeJson.SelectToken("kind").ToObject<string>();
                                if (argumentTypeKind != "SCALAR" && argumentTypeKind != "ENUM"
                                    && argumentTypeKind != "INPUT_OBJECT")
                                {
                                    yield return
                                        $"{typeName} property {fieldName} has argument {argumentName} of type {argumentType} of unsupported kind {argumentTypeKind}";
                                }
                                else if (argumentTypeKind == "INPUT_OBJECT")
                                {
                                    foreach (var error in CheckIntrospectionInputType(argumentTypeJson, types, inputTypesChecked))
                                    {
                                        yield return error;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks input type received via introspection query for correctness
        /// </summary>
        /// <param name="graphType">
        /// The type to check
        /// </param>
        /// <param name="checkedInputTypes">
        /// The types already checked (to limit the recursion)
        /// </param>
        /// <returns>
        /// The list of found errors
        /// </returns>
        private static IEnumerable<string> CheckInputGraphType(
            IComplexGraphType graphType,
            ICollection<string> checkedInputTypes)
        {
            if (checkedInputTypes.Contains(graphType.Name))
            {
                yield break;
            }

            checkedInputTypes.Add(graphType.Name);

            if (graphType.Fields == null || !graphType.Fields.Any())
            {
                yield return $"Input type {graphType.Name} has no fields";
                yield break;
            }

            foreach (var field in graphType.Fields)
            {
                var fieldResolvedType = GetResolvedType(field.ResolvedType);
                if (fieldResolvedType == null)
                {
                    yield return $"Input type {graphType.Name} has field {field.Name} of unknown type";
                    continue;
                }

                if (!(fieldResolvedType is ScalarGraphType) && !(fieldResolvedType is IInputGraphType) && !(fieldResolvedType is InputObjectGraphType))
                {
                    yield return
                        $"Field {field.Name} of type {graphType.Name} has argument {field.Name} has invalid type {fieldResolvedType.Name}";
                    continue;
                }

                var complexGraphType = fieldResolvedType as IComplexGraphType;
                if (complexGraphType != null)
                {
                    foreach (var error in CheckInputGraphType(complexGraphType, checkedInputTypes))
                    {
                        yield return error;
                    }
                }
            }
        }

        /// <summary>
        /// Checks input type received via introspection query for correctness
        /// </summary>
        /// <param name="type">
        /// The type data to check
        /// </param>
        /// <param name="types">
        /// The list of defined types in schema
        /// </param>
        /// <param name="typesChecked">
        /// The types already checked (to limit the recursion)
        /// </param>
        /// <returns>
        /// The list of found errors
        /// </returns>
        private static IEnumerable<string> CheckIntrospectionInputType(
            JObject type,
            IReadOnlyDictionary<JToken, JObject> types,
            ICollection<string> typesChecked)
        {
            var kind = type.SelectToken("kind").ToObject<string>();
            var typeName = type.Property("name").Value.ToObject<string>();
            if (typesChecked.Contains(typeName))
            {
                yield break;
            }

            typesChecked.Add(typeName);

            if (kind != "SCALAR" && kind != "ENUM" && kind != "INPUT_OBJECT")
            {
                yield return $"{typeName} is not a valid input type";
            }

            if (kind != "INPUT_OBJECT")
            {
                yield break;
            }

            var fields = type.Property("inputFields")?.Value as JArray;
            if (fields == null || fields.Count == 0)
            {
                yield return $"{typeName} has no fields";
                yield break;
            }

            foreach (var field in fields)
            {
                var fieldTypeName = field.SelectToken("type.kind")?.ToObject<string>() == "LIST"
                                        ? field.SelectToken("type.ofType.name")?.ToObject<string>()
                                        : field.SelectToken("type.name")?.ToObject<string>();
                var fieldName = field.SelectToken("name")?.ToObject<string>();
                JObject fieldType;
                if (!types.TryGetValue(fieldTypeName ?? string.Empty, out fieldType))
                {
                    yield return $"{typeName} property {fieldName} has unknown type {fieldTypeName}";
                    continue;
                }

                var fieldKind = fieldType.SelectToken("kind").ToObject<string>();
                if (fieldKind != "SCALAR" && fieldKind != "ENUM" && fieldKind != "INPUT_OBJECT")
                {
                    yield return
                        $"Input type {typeName} property {fieldName} of type {fieldTypeName} of unsupported kind {fieldKind}";
                }
                else if (fieldKind == "INPUT_OBJECT")
                {
                    foreach (var error in CheckIntrospectionInputType(fieldType, types, typesChecked))
                    {
                        yield return error;
                    }
                }
            }
        }

        /// <summary>
        /// Resolves end-type
        /// </summary>
        /// <param name="type">The original resolved type</param>
        /// <returns>The end type</returns>
        private static IGraphType GetResolvedType(IGraphType type)
        {
            if (type is ListGraphType)
            {
                type = ((ListGraphType)type).ResolvedType;
            }

            return type;
        }

        /// <summary>
        /// Merges schemes from multiple APIs
        /// </summary>
        /// <param name="providers">
        /// The API providers descriptions
        /// </param>
        /// <param name="createdTypes">
        /// The list of created types.
        /// <remarks>
        /// This is the mutation dictionary and will be filled during creation process
        /// </remarks>
        /// </param>
        /// <returns>
        /// Merged API
        /// </returns>
        private static MergedApiRoot MergeApis(List<ApiProvider> providers, Dictionary<string, MergedType> createdTypes)
        {
            var apiRoot = new MergedApiRoot("api");
            apiRoot.AddProviders(providers.Select(p => new FieldProvider { Provider = p, FieldType = p.Description }));
            apiRoot.Category = providers.Count > 1
                                   ? MergedObjectType.EnCategory.MultipleApiType
                                   : MergedObjectType.EnCategory.SingleApiType;

            foreach (var provider in providers)
            {
                MergeFields(apiRoot, provider.Description.Fields, provider, new List<string>(), false, createdTypes);
                
                foreach (var apiMutation in provider.Description.Mutations)
                {
                    var mutationName = $"{MergedType.EscapeName(provider.Description.ApiName)}_{MergedType.EscapeName(apiMutation.Name)}";
                    MergedField mutation = null;
                    switch (apiMutation.Type)
                    {
                        case ApiMutation.EnType.ConnectionCreate:
                        case ApiMutation.EnType.ConnectionUpdate:
                        case ApiMutation.EnType.ConnectionDelete:
                            mutation = RegisterConnectionMutation(provider, apiMutation, apiRoot, createdTypes);
                            break;
                        case ApiMutation.EnType.Untyped:
                            mutation = RegisterUntypedMutation(provider, apiMutation, apiRoot, createdTypes);
                            break;
                    }

                    if (mutation != null)
                    {
                        apiRoot.Mutations[mutationName] = mutation;
                    }
                }
            }

            var nodeSearcher = new NodeSearcher(apiRoot);
            apiRoot.NodeSearher = nodeSearcher;
            return apiRoot;
        }

        /// <summary>
        /// Register the mutation
        /// </summary>
        /// <param name="provider">The mutation api provider</param>
        /// <param name="apiMutation">The mutation description</param>
        /// <param name="apiRoot">The api root</param>
        /// <param name="typesCreated">The list of created types</param>
        /// <returns>The mutation as merged field </returns>
        private static MergedField RegisterUntypedMutation(
            ApiProvider provider,
            ApiMutation apiMutation,
            MergedApiRoot apiRoot,
            Dictionary<string, MergedType> typesCreated)
        {
            var returnType =
                CreateMergedType(provider, apiMutation, null, new List<string>(), false, typesCreated);

            var inputType = new MergedInputType(apiMutation.Name);
            inputType.AddProvider(new FieldProvider { Provider = provider, FieldType = new ApiObjectType(apiMutation.Name) });
            typesCreated[inputType.ComplexTypeName] = inputType;

            foreach (var apiField in apiMutation.Arguments)
            {
                inputType.Fields.Add(
                    apiField.Name,
                    new MergedField(
                        apiField.Name,
                        CreateMergedType(provider, apiField, null, new List<string>(), true, typesCreated),
                        provider,
                        apiMutation.Clone(),
                        apiMutation.Flags,
                        description: apiField.Description));
            }

            inputType.Fields["clientMutationId"] = new MergedField(
                "clientMutationId",
                CreateScalarType(EnScalarType.String, typesCreated),
                provider,
                apiMutation);

            var arguments = new Dictionary<string, MergedField>
                                {
                                    {
                                        "input",
                                        new MergedField("input", inputType, provider, apiMutation)
                                    }
                                };

            var payload = new MergedUntypedMutationResult(returnType, apiRoot, provider, apiMutation);
            typesCreated[payload.ComplexTypeName] = payload;

            var untypedMutation = new MergedField(
                apiMutation.Name,
                payload,
                provider,
                apiMutation,
                apiMutation.Flags,
                arguments,
                apiMutation.Description);

            return untypedMutation;
        }

        /// <summary>
        /// Register the mutation
        /// </summary>
        /// <param name="provider">The mutation api provider</param>
        /// <param name="apiMutation">The mutation description</param>
        /// <param name="apiRoot">The api root</param>
        /// <param name="typesCreated">The list of created types</param>
        /// <returns>The mutation as merged field </returns>
        private static MergedField RegisterConnectionMutation(
            ApiProvider provider,
            ApiMutation apiMutation,
            MergedApiRoot apiRoot,
            Dictionary<string, MergedType> typesCreated)
        {
            var field = FindContainer(apiMutation, apiRoot);
            var connectionType = field?.Type as MergedConnectionType;
            if (connectionType == null)
            {
                return null;
            }

            var errorDescriptionApiType =
                provider.Description.Types.FirstOrDefault(t => t.TypeName == "ErrorDescription") as ApiObjectType;
            MergedType errorDescriptionType = null;
            if (errorDescriptionApiType != null)
            {
                errorDescriptionType = CreateConnectionType(
                    errorDescriptionApiType,
                    provider,
                    typesCreated);
            }

            var returnType = new MergedConnectionMutationResultType(
                connectionType.ElementType,
                apiRoot,
                errorDescriptionType,
                provider);
            typesCreated[returnType.ComplexTypeName] = returnType;

            var inputType = new MergedInputType(apiMutation.Name);
            inputType.AddProvider(
                new FieldProvider { Provider = provider, FieldType = new ApiObjectType(apiMutation.Name) });
            typesCreated[inputType.ComplexTypeName] = inputType;

            foreach (var apiField in apiMutation.Arguments)
            {
                inputType.Fields.Add(
                    apiField.Name,
                    new MergedField(
                        apiField.Name,
                        CreateMergedType(provider, apiField, null, new List<string>(), true, typesCreated),
                        provider,
                        apiMutation,
                        apiMutation.Flags,
                        description: apiField.Description));
            }

            inputType.Fields["clientMutationId"] = new MergedField(
                "clientMutationId",
                CreateScalarType(EnScalarType.String, typesCreated),
                provider,
                apiMutation);

            var arguments = new Dictionary<string, MergedField>
                                {
                                    {
                                        "input",
                                        new MergedField(
                                            "input",
                                            inputType,
                                            provider,
                                            apiMutation)
                                    }
                                };

            return new MergedField(
                apiMutation.Name,
                returnType,
                provider,
                apiMutation,
                apiMutation.Flags,
                arguments,
                apiMutation.Description);
        }

        /// <summary>
        /// Searches current api for true mutation container
        /// </summary>
        /// <param name="apiMutation">The mutation</param>
        /// <param name="apiRoot">The api root</param>
        /// <returns>The mutation container</returns>
        private static MergedField FindContainer(ApiMutation apiMutation, MergedApiRoot apiRoot)
        {
            var path = apiMutation.Name.Split('.').ToList();
            path.RemoveAt(path.Count - 1);
            MergedObjectType type = apiRoot;
            MergedField field = null;
            var queue = new Queue<string>(path);
            while (queue.Count > 0)
            {
                if (type == null)
                {
                    return null;
                }

                var fieldName = queue.Dequeue();
                if (!type.Fields.TryGetValue(fieldName, out field))
                {
                    return null;
                }

                type = field.Type as MergedObjectType;
            }

            return field;
        }

        /// <summary>
        /// Insert new fields from new provider into current type
        /// </summary>
        /// <param name="parentType">
        /// Field to update
        /// </param>
        /// <param name="apiFields">
        /// The list of subfields from api
        /// </param>
        /// <param name="provider">
        /// The api provider
        /// </param>
        /// <param name="path">
        /// The types names path to avoid circular references.
        /// </param>
        /// <param name="createAsInput">A value indicating that an input type is assembled</param>
        /// <param name="typesCreated">The list of previously created types</param>
        private static void MergeFields(
            MergedObjectType parentType,
            IEnumerable<ApiField> apiFields,
            ApiProvider provider,
            ICollection<string> path,
            bool createAsInput,
            Dictionary<string, MergedType> typesCreated)
        {
            foreach (var apiField in apiFields.Where(f => (createAsInput && f.Flags.HasFlag(EnFieldFlags.CanBeUsedInInput)) || (!createAsInput && f.Flags.HasFlag(EnFieldFlags.Queryable))))
            {
                MergedField complexField;
                if (parentType.Fields.TryGetValue(apiField.Name, out complexField))
                {
                    if (apiField.ScalarType != EnScalarType.None || createAsInput
                        || apiField.Flags.HasFlag(EnFieldFlags.IsConnection)
                        || apiField.Flags.HasFlag(EnFieldFlags.IsArray) || !(complexField.Type is MergedObjectType)
                        || complexField.Arguments.Any() || apiField.Arguments.Any())
                    {
                        // todo: write merge error
                        continue;
                    }
                }

                var fieldType = CreateMergedType(provider, apiField, complexField, path, createAsInput, typesCreated);

                if (fieldType == null)
                {
                    continue;
                }

                var fieldArguments = new Dictionary<string, MergedField>();

                if (!createAsInput)
                {
                    foreach (var argument in apiField.Arguments)
                    {
                        var fieldArgumentType = CreateMergedType(provider, argument, null, path, true, typesCreated);
                        fieldArguments[argument.Name] = new MergedField(
                            argument.Name,
                            fieldArgumentType,
                            provider,
                            apiField,
                            argument.Flags,
                            description: argument.Description);
                    }
                }

                var description = string.Join(
                    "\n",
                    new[] { complexField?.Description, apiField.Description }.Where(s => !string.IsNullOrWhiteSpace(s)));
                var field = new MergedField(
                    apiField.Name,
                    fieldType,
                    provider,
                    apiField,
                    apiField.Flags,
                    fieldArguments,
                    string.IsNullOrWhiteSpace(description) ? null : description);
                if (complexField != null)
                {
                    foreach (var complexFieldProvider in complexField.Providers)
                    {
                        field.AddProvider(
                            complexFieldProvider,
                            complexField.OriginalFields[complexFieldProvider.Description.ApiName]);
                    }
                }

                parentType.Fields[apiField.Name] = field;
            }
        }

        /// <summary>
        /// Creates field from api description
        /// </summary>
        /// <param name="provider">The api provider</param>
        /// <param name="apiField">The api field description</param>
        /// <param name="complexField">The same field merged from previous api descriptions</param>
        /// <param name="path">The list of processed types</param>
        /// <param name="createAsInput">A value indicating that an input type is assembled</param>
        /// <param name="typesCreated">The list of already created types</param>
        /// <returns>The field description</returns>
        private static MergedType CreateMergedType(
            ApiProvider provider,
            ApiField apiField,
            MergedField complexField,
            ICollection<string> path,
            bool createAsInput,
            Dictionary<string, MergedType> typesCreated)
        {
            MergedType createdType;
            if (apiField.ScalarType != EnScalarType.None)
            {
                return CreateScalarType(apiField.ScalarType, typesCreated);
            }

            var apiType = provider.Description.Types.FirstOrDefault(t => t.TypeName == apiField.TypeName);
            if (apiType == null)
            {
                throw new Exception("type was not found");
            }

            var apiEnumType = apiType as ApiEnumType;
            if (apiEnumType != null)
            {
                return CreateEnumType(apiEnumType, provider, typesCreated);
            }

            var apiObjectType = (ApiObjectType)apiType;
            if (apiField.Flags.HasFlag(EnFieldFlags.IsConnection))
            {
                return CreateConnectionType(apiObjectType, provider, typesCreated);
            }

            var objectType = (complexField?.Type as MergedObjectType)?.Clone()
                             ?? (createAsInput
                                     ? new MergedInputType($"{provider.Description.ApiName}_{apiField.TypeName}")
                                     : new MergedObjectType($"{provider.Description.ApiName}_{apiField.TypeName}"));
            objectType.AddProvider(new FieldProvider { FieldType = apiObjectType, Provider = provider });
            if (complexField != null)
            {
                objectType.Category = MergedObjectType.EnCategory.MultipleApiType;
            }

            if (typesCreated.TryGetValue(objectType.ComplexTypeName, out createdType))
            {
                return createdType;
            }

            typesCreated[objectType.ComplexTypeName] = objectType;

            var fieldsToMerge = createAsInput
                                    ? apiObjectType.Fields.Where(
                                        f => !f.Flags.HasFlag(EnFieldFlags.IsConnection) && !f.Arguments.Any())
                                    : apiObjectType.Fields;

            MergeFields(
                objectType,
                fieldsToMerge,
                provider,
                path.Union(new[] { apiObjectType.TypeName }).ToList(),
                createAsInput,
                typesCreated);

            objectType.Initialize();
            return objectType;
        }

        /// <summary>
        /// Creates a connection type
        /// </summary>
        /// <param name="apiObjectType">The node object type</param>
        /// <param name="provider">The api provider</param>
        /// <param name="typesCreated">The list of types created to fill</param>
        /// <returns>The connection type</returns>
        private static MergedType CreateConnectionType(
            ApiObjectType apiObjectType,
            ApiProvider provider,
            Dictionary<string, MergedType> typesCreated)
        {
            MergedType createdType;
            var nodeType =
                (MergedObjectType)
                CreateMergedType(provider, apiObjectType.CreateField("node"), null, new List<string>(), false, typesCreated);

            var connectionType = new MergedConnectionType(nodeType.OriginalTypeName, provider, nodeType);

            if (typesCreated.TryGetValue(connectionType.ComplexTypeName, out createdType))
            {
                return createdType;
            }

            typesCreated[connectionType.ComplexTypeName] = connectionType;
            typesCreated[connectionType.EdgeType.ComplexTypeName] = connectionType.EdgeType;
            typesCreated[connectionType.ElementType.ComplexTypeName] = connectionType.ElementType;
            return connectionType;
        }

        /// <summary>
        /// Creates the enum type definition
        /// </summary>
        /// <param name="apiEnumType">Api enum type definition</param>
        /// <param name="provider">The api provider</param>
        /// <param name="typesCreated">The list of types created to fill</param>
        /// <returns>The enum type</returns>
        private static MergedType CreateEnumType(ApiEnumType apiEnumType, ApiProvider provider, Dictionary<string, MergedType> typesCreated)
        {
            MergedType createdType;
            var mergedEnumType = new MergedEnumType(apiEnumType, provider);
            if (typesCreated.TryGetValue(mergedEnumType.ComplexTypeName, out createdType))
            {
                return createdType;
            }

            typesCreated[mergedEnumType.ComplexTypeName] = mergedEnumType;

            return mergedEnumType;
        }

        /// <summary>
        /// Creates the scalar type definition
        /// </summary>
        /// <param name="scalarType">The type of scalar</param>
        /// <param name="typesCreated">The list of types created to fill</param>
        /// <returns>The scalar type</returns>
        private static MergedType CreateScalarType(EnScalarType scalarType, Dictionary<string, MergedType> typesCreated)
        {
            MergedType createdType;
            var mergedScalarType = new MergedScalarType(scalarType);

            if (typesCreated.TryGetValue(mergedScalarType.ComplexTypeName, out createdType))
            {
                return createdType;
            }

            typesCreated[mergedScalarType.ComplexTypeName] = mergedScalarType;
            return mergedScalarType;
        }
    }
}