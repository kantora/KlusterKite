// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ApiProviderPublishResolveIntegration.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Testing publishing and resolving integration
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.Tests.GraphQL
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using global::GraphQL;
    using global::GraphQL.NewtonsoftJson;
    using global::GraphQL.Utilities;

    using JetBrains.Annotations;

    using KlusterKite.API.Attributes;
    using KlusterKite.API.Client;
    using KlusterKite.API.Tests.Mock;
    using KlusterKite.Core;
    using KlusterKite.Core.Log;
    using KlusterKite.Security.Attributes;

    using KlusterKite.Web.GraphQL.Publisher;
    using KlusterKite.Web.GraphQL.Publisher.Internals;


    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using Serilog;
    using Serilog.Core;
    using Serilog.Events;

    using Xunit;
    using Xunit.Abstractions;

    using Constants = KlusterKite.Core.Log.Constants;

    /// <summary>
    /// Testing publishing and resolving integration
    /// </summary>
    public class ApiProviderPublishResolveIntegration
    {
        /// <summary>
        /// The output.
        /// </summary>
        private readonly ITestOutputHelper output;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiProviderPublishResolveIntegration"/> class.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        public ApiProviderPublishResolveIntegration(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Removes formatting values from response json
        /// </summary>
        /// <param name="response">The json string</param>
        /// <returns>Cleaned json string</returns>
        public static string CleanResponse(string response)
        {
            return JsonConvert.DeserializeObject<JObject>(response).ToString(Formatting.None);
        }



        /// <summary>
        /// Testing connection query request from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ConnectionQueryTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{B500CA20-F649-4DCD-BDA8-1FA5031ECDD3}"),
                                                 Name = "2-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{67885BA0-B284-438F-8393-EE9A9EB299D1}"),
                                                 Name = "3-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3AF2C973-D985-4F95-A0C7-AA928D276881}"),
                                                 Name = "4-test",
                                                 Value = 70m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{F0607502-5B77-4A3C-9142-E6197A7EE61E}"),
                                                 Name = "5-test",
                                                 Value = 6m
                                             },
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            {                
                api {
                        connection(sort: [value_asc, name_asc], filter: {value_gt: 10}, offset: 1, limit: 2) {
                            count,
                            edges {
                                cursor,
                                node {                                    
                                    _id,
                                    name,
                                    value
                                }                    
                            }
                        }
                }                
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                     /*
                                     r.ComplexityConfiguration = new ComplexityConfiguration
                                                                     {
                                                                         FieldImpact = 2.0,
                                                                         MaxDepth = 15,
                                                                         MaxComplexity = 200
                                                                     };
                                     */
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var expectedResult = @"
                            {
                              ""data"": {
                                ""api"": {
                                  ""connection"": {
                                    ""count"": 4,
                                    ""edges"": [
                                      {
                                        ""cursor"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                        ""node"": {
                                          ""_id"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                          ""name"": ""3-test"",
                                          ""value"": 50.0
                                        }
                                      },
                                      {
                                        ""cursor"": ""3af2c973-d985-4f95-a0c7-aa928d276881"",
                                        ""node"": {                                          
                                          ""_id"": ""3af2c973-d985-4f95-a0c7-aa928d276881"",
                                          ""name"": ""4-test"",
                                          ""value"": 70.0
                                        }
                                      }
                                    ]
                                  }
                                }
                              }
                            }
                            ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing connection method query request from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ConnectionMethodQueryTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             }
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });
            var globalId =
                JArray.Parse(
                    "[{\"f\":\"connectionMethod\", \"a\": {\"key\": \"test\", \"obj\": {\"name\": \"hello\"}}, \"id\":\"3beee369-11df-4a30-bf11-1d8465c87110" + "\"}]")
                    .PackGlobalId().Replace("\"", "\\\"");
            var query = @"
            {                
                api {
                        connectionMethod(key: ""test"", obj: {name: ""hello""}, sort: [value_asc, name_asc]) {
                            count,
                            edges {
                                cursor,
                                node { 
                                    id,                                   
                                    __id,
                                    name,
                                    value
                                }                    
                            }
                        }
                }                
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 /*
                                 r.ComplexityConfiguration = new ComplexityConfiguration
                                 {
                                     FieldImpact = 2.0,
                                     MaxDepth = 15,
                                     MaxComplexity = 200
                                 };
                                 */
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var expectedResult = $@"
                            {{
                              ""data"": {{
                                ""api"": {{
                                  ""connectionMethod"": {{
                                    ""count"": 1,
                                    ""edges"": [
                                      {{
                                        ""cursor"": ""3beee369-11df-4a30-bf11-1d8465c87110"",
                                        ""node"": {{
                                          ""id"": ""{globalId}"",
                                          ""__id"": ""3beee369-11df-4a30-bf11-1d8465c87110"",
                                          ""name"": ""1-test"",
                                          ""value"": 100.0
                                        }}
                                      }}
                                    ]
                                  }}
                                }}
                              }}
                            }}
                            ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing connection query request from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ConnectionQueryWithDoubleFragmentsTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             }
                                     };

            var internalApiProvider = new TestProvider(initialObjects);

            var globalId =
            JArray.Parse(
                "[{\"f\":\"listOfNested\"," + $"\"id\":\"{internalApiProvider.ListOfNested.First().Uid.ToString("D")}" + "\"},"
                + "{\"f\":\"connection\", \"id\":\"3beee369-11df-4a30-bf11-1d8465c87110" + "\"}]")
                .PackGlobalId().Replace("\"", "\\\"");

            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            query {                
                api {
                    ...F0
                    ...F1
                }                
            }

            fragment F0 on ITestApi {
                listOfNested {
                    edges {
                        node {
                            syncScalarField
                        }
                    }
                }
            }
            fragment F1 on ITestApi {
                listOfNested {
                    edges {
                        node {
                            connection {
                                edges {
                                    node {
                                        id,
                                        name
                                    }
                                }
                            }
                        }
                    }
                }
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var expectedResult = $@"
                            {{
                              ""data"": {{
                                ""api"": {{
                                  ""listOfNested"": {{
                                    ""edges"": [
                                      {{
                                        ""node"": {{
                                          ""syncScalarField"": ""SyncScalarField"",
                                          ""connection"": {{
                                            ""edges"": [
                                              {{
                                                ""node"": {{
                                                  ""id"": ""{globalId}"",
                                                  ""name"": ""1-test""
                                                }}
                                              }}
                                            ]
                                          }}
                                        }}
                                      }}
                                    ]
                                  }}
                                }}
                              }}
                            }}
                            ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing connection query split request from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ConnectionQuerySplitRequestTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{B500CA20-F649-4DCD-BDA8-1FA5031ECDD3}"),
                                                 Name = "2-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{67885BA0-B284-438F-8393-EE9A9EB299D1}"),
                                                 Name = "3-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3AF2C973-D985-4F95-A0C7-AA928D276881}"),
                                                 Name = "4-test",
                                                 Value = 70m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{F0607502-5B77-4A3C-9142-E6197A7EE61E}"),
                                                 Name = "5-test",
                                                 Value = 6m
                                             },
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            {                
                api {
                        connection(sort: [value_asc, name_asc], filter: {value_gt: 10}, offset: 1, limit: 2) {
                            count,
                            edges {
                                cursor,
                                node {                                    
                                    __id,
                                    name
                                }                    
                            }
                            edges {                                
                                node {                                    
                                    value
                                }                    
                            }
                        }
                }                
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 /*
                                 r.ComplexityConfiguration = new ComplexityConfiguration
                                 {
                                     FieldImpact = 2.0,
                                     MaxDepth = 15,
                                     MaxComplexity = 200
                                 };
                                 */
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var expectedResult = @"
                            {
                              ""data"": {
                                ""api"": {
                                  ""connection"": {
                                    ""count"": 4,
                                    ""edges"": [
                                      {
                                        ""cursor"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                        ""node"": {
                                          ""__id"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                          ""name"": ""3-test"",
                                          ""value"": 50.0
                                        }
                                      },
                                      {
                                        ""cursor"": ""3af2c973-d985-4f95-a0c7-aa928d276881"",
                                        ""node"": {                                          
                                          ""__id"": ""3af2c973-d985-4f95-a0c7-aa928d276881"",
                                          ""name"": ""4-test"",
                                          ""value"": 70.0
                                        }
                                      }
                                    ]
                                  }
                                }
                              }
                            }
                            ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing connection query request from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ConnectionQueryWithAliasesTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{B500CA20-F649-4DCD-BDA8-1FA5031ECDD3}"),
                                                 Name = "2-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{67885BA0-B284-438F-8393-EE9A9EB299D1}"),
                                                 Name = "3-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3AF2C973-D985-4F95-A0C7-AA928D276881}"),
                                                 Name = "4-test",
                                                 Value = 70m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{F0607502-5B77-4A3C-9142-E6197A7EE61E}"),
                                                 Name = "5-test",
                                                 Value = 6m
                                             },
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            {                
                api {
                        list: connection(sort: [value_asc, name_asc], filter: {value_gt: 10}, offset: 1, limit: 2) {
                            total: count,
                            total2: count,
                            items: edges {
                                location: cursor,
                                location2: cursor,
                                item: node {                                    
                                    naming: name,
                                },
                                item2: node {
                                    id2: _id,
                                    v2: value
                                },                      
                            },
                            items2: edges {
                                location: cursor,
                                location2: cursor,
                                item: node {
                                    type
                                },
                                item2: node {
                                    id2: _id,
                                    naming2: name,
                                    v2: value,
                                    type
                                },                      
                            }
                        }
                }                
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var expectedResult = @"
                                    {
                                      ""data"": {
                                        ""api"": {
                                          ""list"": {
                                            ""total"": 4,
                                            ""total2"": 4,
                                            ""items"": [
                                              {
                                                ""location"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                                ""location2"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                                ""item"": {
                                                  ""naming"": ""3-test""
                                                },
                                                ""item2"": {
                                                  ""id2"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                                  ""v2"": 50.0
                                                }
                                              },
                                              {
                                                ""location"": ""3af2c973-d985-4f95-a0c7-aa928d276881"",
                                                ""location2"": ""3af2c973-d985-4f95-a0c7-aa928d276881"",
                                                ""item"": {
                                                  ""naming"": ""4-test""
                                                },
                                                ""item2"": {
                                                  ""id2"": ""3af2c973-d985-4f95-a0c7-aa928d276881"",
                                                  ""v2"": 70.0
                                                }
                                              }
                                            ],
                                            ""items2"": [
                                              {
                                                ""location"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                                ""location2"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                                ""item"": {
                                                  ""type"": ""Good""
                                                },
                                                ""item2"": {
                                                  ""id2"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                                  ""naming2"": ""3-test"",
                                                  ""v2"": 50.0,
                                                  ""type"": ""Good""
                                                }
                                              },
                                              {
                                                ""location"": ""3af2c973-d985-4f95-a0c7-aa928d276881"",
                                                ""location2"": ""3af2c973-d985-4f95-a0c7-aa928d276881"",
                                                ""item"": {
                                                  ""type"": ""Good""
                                                },
                                                ""item2"": {
                                                  ""id2"": ""3af2c973-d985-4f95-a0c7-aa928d276881"",
                                                  ""naming2"": ""4-test"",
                                                  ""v2"": 70.0,
                                                  ""type"": ""Good""
                                                }
                                              }
                                            ]
                                          }
                                        }
                                      }
                                    }
                                ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing connection create request from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ConnectionMutationInsertTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{B500CA20-F649-4DCD-BDA8-1FA5031ECDD3}"),
                                                 Name = "2-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{67885BA0-B284-438F-8393-EE9A9EB299D1}"),
                                                 Name = "3-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3AF2C973-D985-4F95-A0C7-AA928D276881}"),
                                                 Name = "4-test",
                                                 Value = 70m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{F0607502-5B77-4A3C-9142-E6197A7EE61E}"),
                                                 Name = "5-test",
                                                 Value = 6m
                                             },
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var newItemGlobalId =
                JArray.Parse("[{\"f\": \"connection\", \"id\":\"251feea8-d3ac-461d-a385-0cf2ba7a74e8\"}]")
                    .PackGlobalId().Replace("\"", "\\\"");

            var query = @"                          
            mutation M {
                    call: testApi_connection_create(input: {newNode: {id: ""251FEEA8-D3AC-461D-A385-0CF2BA7A74E8"", name: ""hello world"", value: 13}, clientMutationId: ""testClientMutationId""}) {
                    clientMutationId,
                    node {
                        id,
                        __id,
                        name,
                        value
                    },
                    edge {
                        cursor,
                        node {
                            id,
                            __id,
                            name,
                            value
                        }
                    },
                    deletedId,
                    api {
                        connection(sort: [value_asc, name_asc], filter: {value: 13}) {
                            count,
                            edges {
                                cursor,
                                node {
                                    id,
                                    __id,
                                    name,
                                    value
                                }                    
                            }
                        }
                    }
                }
            }            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = $@"
                                    {{
                                      ""data"": {{
                                        ""call"": {{
                                          ""clientMutationId"": ""testClientMutationId"",
                                          ""node"": {{
                                            ""id"": ""{newItemGlobalId}"",
                                            ""__id"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                            ""name"": ""hello world"",
                                            ""value"": 13.0
                                          }},
                                          ""edge"": {{
                                            ""cursor"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                            ""node"": {{
                                              ""id"": ""{newItemGlobalId}"",
                                              ""__id"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                              ""name"": ""hello world"",
                                              ""value"": 13.0
                                            }}
                                          }},
                                          ""deletedId"": null,
                                          ""api"": {{
                                            ""connection"": {{
                                              ""count"": 1,
                                              ""edges"": [
                                                {{
                                                  ""cursor"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                                  ""node"": {{
                                                    ""id"": ""{newItemGlobalId}"",
                                                    ""__id"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                                    ""name"": ""hello world"",
                                                    ""value"": 13.0
                                                  }}
                                                }}
                                              ]
                                            }}
                                          }}
                                        }}
                                      }}
                                    }}
                                    ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing connection create request from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ConnectionMutationInsertWithAliasesTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{B500CA20-F649-4DCD-BDA8-1FA5031ECDD3}"),
                                                 Name = "2-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{67885BA0-B284-438F-8393-EE9A9EB299D1}"),
                                                 Name = "3-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3AF2C973-D985-4F95-A0C7-AA928D276881}"),
                                                 Name = "4-test",
                                                 Value = 70m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{F0607502-5B77-4A3C-9142-E6197A7EE61E}"),
                                                 Name = "5-test",
                                                 Value = 6m
                                             },
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var nodeId = JArray.Parse("[{\"f\": \"connection\", \"id\":\"251feea8-d3ac-461d-a385-0cf2ba7a74e8\"}]").PackGlobalId().Replace("\"", "\\\"");

            var query = @"                          
            mutation M {
                    call: testApi_connection_create(input: {newNode: {id: ""251FEEA8-D3AC-461D-A385-0CF2BA7A74E8"", name: ""hello world"", value: 13}, clientMutationId: ""testClientMutationId""}) {
                    mutationId: clientMutationId,
                    nodeElement: node {
                        globalId: id,
                        realId: __id,
                        elementName: name,
                        value
                    },
                    nodeElement2: node {
                        globalId2: id,
                        realId2: __id,
                        elementName2: name,
                        value
                    },
                    edgeObject: edge {
                        id: cursor,
                        object: node {
                            longId: id,
                            edgeName: name,
                        },
                        object2: node {
                            shortId: __id,
                            value
                        },
                    },
                    edgeObject2: edge {
                        id2: cursor,
                        object2: node {
                            longId2: id,
                            shortId2: __id,
                            edgeName2: name,
                            value
                        }
                    },
                    removedId: deletedId,
                    root: api {
                        apiObjects: connection(sort: [value_asc, name_asc], filter: {value: 13}) {
                            total: count,
                            list: edges {
                                itemId: cursor,
                                element: node {
                                    elementGlobalId: id,
                                    elementId: __id,
                                    elementName: name,
                                    elementValue: value
                                }             
                            }
                        }
                    }
                }
            }            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = $@"
                                    {{
                                      ""data"": {{
                                        ""call"": {{
                                          ""mutationId"": ""testClientMutationId"",
                                          ""nodeElement"": {{
                                            ""globalId"": ""{nodeId}"",
                                            ""realId"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                            ""elementName"": ""hello world"",
                                            ""value"": 13.0
                                          }},
                                          ""nodeElement2"": {{
                                            ""globalId2"": ""{nodeId}"",
                                            ""realId2"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                            ""elementName2"": ""hello world"",
                                            ""value"": 13.0
                                          }},
                                          ""edgeObject"": {{
                                            ""id"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                            ""object"": {{
                                              ""longId"": ""{nodeId}"",
                                              ""edgeName"": ""hello world""
                                            }},
                                            ""object2"": {{
                                              ""shortId"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                              ""value"": 13.0
                                            }}
                                          }},
                                          ""edgeObject2"": {{
                                            ""id2"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                            ""object2"": {{
                                              ""longId2"": ""{nodeId}"",
                                              ""shortId2"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                              ""edgeName2"": ""hello world"",
                                              ""value"": 13.0
                                            }}
                                          }},
                                          ""removedId"": null,
                                          ""root"": {{
                                            ""apiObjects"": {{
                                              ""total"": 1,
                                              ""list"": [
                                                {{
                                                  ""itemId"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                                  ""element"": {{
                                                    ""elementGlobalId"": ""{nodeId}"",
                                                    ""elementId"": ""251feea8-d3ac-461d-a385-0cf2ba7a74e8"",
                                                    ""elementName"": ""hello world"",
                                                    ""elementValue"": 13.0
                                                  }}
                                                }}
                                              ]
                                            }}
                                          }}
                                        }}
                                      }}
                                    }}
                                    ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing connection delete request from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ConnectionMutationDeleteTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{B500CA20-F649-4DCD-BDA8-1FA5031ECDD3}"),
                                                 Name = "2-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{67885BA0-B284-438F-8393-EE9A9EB299D1}"),
                                                 Name = "3-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3AF2C973-D985-4F95-A0C7-AA928D276881}"),
                                                 Name = "4-test",
                                                 Value = 70m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{F0607502-5B77-4A3C-9142-E6197A7EE61E}"),
                                                 Name = "5-test",
                                                 Value = 6m
                                             },
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var node1Id = JArray.Parse("[{\"f\": \"connection\", \"id\":\"3beee369-11df-4a30-bf11-1d8465c87110\"}]").PackGlobalId().Replace("\"", "\\\"");
            var node2Id = JArray.Parse("[{\"f\": \"connection\", \"id\":\"b500ca20-f649-4dcd-bda8-1fa5031ecdd3\"}]").PackGlobalId().Replace("\"", "\\\"");
            var node3Id = JArray.Parse("[{\"f\": \"connection\", \"id\":\"67885ba0-b284-438f-8393-ee9a9eb299d1\"}]").PackGlobalId().Replace("\"", "\\\"");
            var node4Id = JArray.Parse("[{\"f\": \"connection\", \"id\":\"3af2c973-d985-4f95-a0c7-aa928d276881\"}]").PackGlobalId().Replace("\"", "\\\"");
            var node5Id = JArray.Parse("[{\"f\": \"connection\", \"id\":\"f0607502-5b77-4a3c-9142-e6197a7ee61e\"}]").PackGlobalId().Replace("\"", "\\\"");

            var query = @"                          
            mutation M {
                    call: testApi_connection_delete(input: {id: ""3BEEE369-11DF-4A30-BF11-1D8465C87110""}) {
                    node {
                        id,
                        __id,
                        name,
                        value
                    },
                    edge {
                        cursor,
                        node {
                            id,
                            __id,
                            name,
                            value
                        }
                    },
                    deletedId,
                    api {
                        connection(sort: [value_asc, name_asc]) {
                            count,
                            edges {
                                cursor,
                                node {
                                    id,
                                    __id,
                                    name,
                                    value
                                }                    
                            }
                        }
                    }
                }
            }            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = $@"
                                {{
                                  ""data"": {{
                                    ""call"": {{
                                      ""node"": {{
                                        ""id"": ""{node1Id}"",
                                        ""__id"": ""3beee369-11df-4a30-bf11-1d8465c87110"",
                                        ""name"": ""1-test"",
                                        ""value"": 100.0
                                      }},
                                      ""edge"": {{
                                        ""cursor"": ""3beee369-11df-4a30-bf11-1d8465c87110"",
                                        ""node"": {{
                                          ""id"": ""{node1Id}"",
                                          ""__id"": ""3beee369-11df-4a30-bf11-1d8465c87110"",
                                          ""name"": ""1-test"",
                                          ""value"": 100.0
                                        }}
                                      }},
                                      ""deletedId"": ""{node1Id}"",
                                      ""api"": {{
                                        ""connection"": {{
                                          ""count"": 4,
                                          ""edges"": [
                                            {{
                                              ""cursor"": ""f0607502-5b77-4a3c-9142-e6197a7ee61e"",
                                              ""node"": {{
                                                ""id"": ""{node5Id}"",
                                                ""__id"": ""f0607502-5b77-4a3c-9142-e6197a7ee61e"",
                                                ""name"": ""5-test"",
                                                ""value"": 6.0
                                              }}
                                            }},
                                            {{
                                              ""cursor"": ""b500ca20-f649-4dcd-bda8-1fa5031ecdd3"",
                                              ""node"": {{
                                                ""id"": ""{node2Id}"",
                                                ""__id"": ""b500ca20-f649-4dcd-bda8-1fa5031ecdd3"",
                                                ""name"": ""2-test"",
                                                ""value"": 50.0
                                              }}
                                            }},
                                            {{
                                              ""cursor"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                              ""node"": {{
                                                ""id"": ""{node3Id}"",
                                                ""__id"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                                ""name"": ""3-test"",
                                                ""value"": 50.0
                                              }}
                                            }},
                                            {{
                                              ""cursor"": ""3af2c973-d985-4f95-a0c7-aa928d276881"",
                                              ""node"": {{
                                                ""id"": ""{node4Id}"",
                                                ""__id"": ""3af2c973-d985-4f95-a0c7-aa928d276881"",
                                                ""name"": ""4-test"",
                                                ""value"": 70.0
                                              }}
                                            }}
                                          ]
                                        }}
                                      }}
                                    }}
                                  }}
                                }}";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing connection delete request from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ConnectionMutationAdditionalTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             }
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var node1Id = JArray.Parse("[{\"f\": \"connection\", \"id\":\"3beee369-11df-4a30-bf11-1d8465c87110\"}]").PackGlobalId().Replace("\"", "\\\"");

            var query = @"                          
            mutation M {
                    call: testApi_connection_typedMutation(input: {uid: ""3BEEE369-11DF-4A30-BF11-1D8465C87110""}) {
                    node {
                        id,
                        __id,
                        name,
                        value
                    },
                    edge {
                        cursor,
                        node {
                            id,
                            __id,
                            name,
                            value
                        }
                    },
                    deletedId,
                    api {
                        connection(sort: [value_asc, name_asc]) {
                            count,
                            edges {
                                cursor,
                                node {
                                    id,
                                    __id,
                                    name,
                                    value
                                }                    
                            }
                        }
                    }
                }
            }            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = $@"
                                {{
                                  ""data"": {{
                                    ""call"": {{
                                      ""node"": {{
                                        ""id"": ""{node1Id}"",
                                        ""__id"": ""3beee369-11df-4a30-bf11-1d8465c87110"",
                                        ""name"": ""1-test"",
                                        ""value"": 100.0
                                      }},
                                      ""edge"": {{
                                        ""cursor"": ""3beee369-11df-4a30-bf11-1d8465c87110"",
                                        ""node"": {{
                                          ""id"": ""{node1Id}"",
                                          ""__id"": ""3beee369-11df-4a30-bf11-1d8465c87110"",
                                          ""name"": ""1-test"",
                                          ""value"": 100.0
                                        }}
                                      }},
                                      ""deletedId"": null,
                                      ""api"": {{
                                        ""connection"": {{
                                          ""count"": 1,
                                          ""edges"": [
                                            {{
                                              ""cursor"": ""3beee369-11df-4a30-bf11-1d8465c87110"",
                                              ""node"": {{
                                                ""id"": ""{node1Id}"",
                                                ""__id"": ""3beee369-11df-4a30-bf11-1d8465c87110"",
                                                ""name"": ""1-test"",
                                                ""value"": 100.0
                                              }}
                                            }}
                                          ]
                                        }}
                                      }}
                                    }}
                                  }}
                                }}";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing connection update request from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ConnectionMutationUpdateTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{B500CA20-F649-4DCD-BDA8-1FA5031ECDD3}"),
                                                 Name = "2-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{67885BA0-B284-438F-8393-EE9A9EB299D1}"),
                                                 Name = "3-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3AF2C973-D985-4F95-A0C7-AA928D276881}"),
                                                 Name = "4-test",
                                                 Value = 70m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{F0607502-5B77-4A3C-9142-E6197A7EE61E}"),
                                                 Name = "5-test",
                                                 Value = 6m
                                             },
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var oldId = JArray.Parse("[{\"f\": \"connection\", \"id\":\"3beee369-11df-4a30-bf11-1d8465c87110\"}]")
        .PackGlobalId().Replace("\"", "\\\"");

            var newId = JArray.Parse("[{\"f\": \"connection\", \"id\":\"3beee369-11df-4a30-bf11-1d8465c87111\"}]")
.PackGlobalId().Replace("\"", "\\\"");

            var query = @"                          
            mutation M {
                    call: testApi_connection_update(input: {id: ""3BEEE369-11DF-4A30-BF11-1D8465C87110"", newNode: {id: ""3BEEE369-11DF-4A30-BF11-1D8465C87111"", name: ""hello world"", value: 13}}) {
                    node {
                        id,
                        __id,
                        name,
                        value
                    },
                    edge {
                        cursor,
                        node {
                            id,
                            __id,
                            name,
                            value
                        }
                    },
                    deletedId,
                    api {
                        connection(sort: [value_asc, name_asc], filter: {value: 13}) {
                            count,
                            edges {
                                cursor,
                                node {
                                    id,
                                    __id,
                                    name,
                                    value
                                }                    
                            }
                        }
                    }
                }
            }            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = $@"
                                    {{
                                      ""data"": {{
                                        ""call"": {{
                                          ""node"": {{
                                            ""id"": ""{newId}"",
                                            ""__id"": ""3beee369-11df-4a30-bf11-1d8465c87111"",
                                            ""name"": ""hello world"",
                                            ""value"": 13.0
                                          }},
                                          ""edge"": {{
                                            ""cursor"": ""3beee369-11df-4a30-bf11-1d8465c87111"",
                                            ""node"": {{
                                              ""id"": ""{newId}"",
                                              ""__id"": ""3beee369-11df-4a30-bf11-1d8465c87111"",
                                              ""name"": ""hello world"",
                                              ""value"": 13.0
                                            }}
                                          }},
                                          ""deletedId"": ""{oldId}"",
                                          ""api"": {{
                                            ""connection"": {{
                                              ""count"": 1,
                                              ""edges"": [
                                                {{
                                                  ""cursor"": ""3beee369-11df-4a30-bf11-1d8465c87111"",
                                                  ""node"": {{
                                                    ""id"": ""{newId}"",
                                                    ""__id"": ""3beee369-11df-4a30-bf11-1d8465c87111"",
                                                    ""name"": ""hello world"",
                                                    ""value"": 13.0
                                                  }}
                                                }}
                                              ]
                                            }}
                                          }}
                                        }}
                                      }}
                                    }}
                                ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing connection update request from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ConnectionMutationNestedUpdateTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{B500CA20-F649-4DCD-BDA8-1FA5031ECDD3}"),
                                                 Name = "2-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{67885BA0-B284-438F-8393-EE9A9EB299D1}"),
                                                 Name = "3-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3AF2C973-D985-4F95-A0C7-AA928D276881}"),
                                                 Name = "4-test",
                                                 Value = 70m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{F0607502-5B77-4A3C-9142-E6197A7EE61E}"),
                                                 Name = "5-test",
                                                 Value = 6m
                                             },
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var oldId = JArray.Parse("[{\"f\": \"nestedSync\"},{\"f\": \"connection\", \"id\":\"3beee369-11df-4a30-bf11-1d8465c87110\"}]")
        .PackGlobalId().Replace("\"", "\\\"");

            var newId = JArray.Parse("[{\"f\": \"nestedSync\"},{\"f\": \"connection\", \"id\":\"3beee369-11df-4a30-bf11-1d8465c87111\"}]")
.PackGlobalId().Replace("\"", "\\\"");
            var shortId = JArray.Parse("[{\"f\": \"connection\", \"id\":\"3beee369-11df-4a30-bf11-1d8465c87111\"}]")
.PackGlobalId().Replace("\"", "\\\"");

            var query = @"                          
            mutation M {
                    call: testApi_nestedSync_connection_update(input: {id: ""3BEEE369-11DF-4A30-BF11-1D8465C87110"", newNode: {id: ""3BEEE369-11DF-4A30-BF11-1D8465C87111"", name: ""hello world"", value: 13}}) {
                    node {
                        id,
                        __id,
                        name,
                        value
                    },
                    edge {
                        cursor,
                        node {
                            id,
                            __id,
                            name,
                            value
                        }
                    },
                    deletedId,
                    api {
                        connection(sort: [value_asc, name_asc], filter: {value: 13}) {
                            count,
                            edges {
                                cursor,
                                node {
                                    id,
                                    __id,
                                    name,
                                    value
                                }                    
                            }
                        }
                    }
                }
            }            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = $@"
                                    {{
                                      ""data"": {{
                                        ""call"": {{
                                          ""node"": {{
                                            ""id"": ""{newId}"",
                                            ""__id"": ""3beee369-11df-4a30-bf11-1d8465c87111"",
                                            ""name"": ""hello world"",
                                            ""value"": 13.0
                                          }},
                                          ""edge"": {{
                                            ""cursor"": ""3beee369-11df-4a30-bf11-1d8465c87111"",
                                            ""node"": {{
                                              ""id"": ""{newId}"",
                                              ""__id"": ""3beee369-11df-4a30-bf11-1d8465c87111"",
                                              ""name"": ""hello world"",
                                              ""value"": 13.0
                                            }}
                                          }},
                                          ""deletedId"": ""{oldId}"",
                                          ""api"": {{
                                            ""connection"": {{
                                              ""count"": 1,
                                              ""edges"": [
                                                {{
                                                  ""cursor"": ""3beee369-11df-4a30-bf11-1d8465c87111"",
                                                  ""node"": {{
                                                    ""id"": ""{shortId}"",
                                                    ""__id"": ""3beee369-11df-4a30-bf11-1d8465c87111"",
                                                    ""name"": ""hello world"",
                                                    ""value"": 13.0
                                                  }}
                                                }}
                                              ]
                                            }}
                                          }}
                                        }}
                                      }}
                                    }}
                                ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing connection update request from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ConnectionMutationFaultedUpdateTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{B500CA20-F649-4DCD-BDA8-1FA5031ECDD3}"),
                                                 Name = "2-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{67885BA0-B284-438F-8393-EE9A9EB299D1}"),
                                                 Name = "3-test",
                                                 Value = 50m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3AF2C973-D985-4F95-A0C7-AA928D276881}"),
                                                 Name = "4-test",
                                                 Value = 70m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{F0607502-5B77-4A3C-9142-E6197A7EE61E}"),
                                                 Name = "5-test",
                                                 Value = 6m
                                             },
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"                          
            mutation M {
                    call: testApi_connection_update(input: {id: ""3BEEE369-11DF-4A30-BF11-1D8465C87111"", newNode: { name: ""hello world"", value: 13}}) {
                    errors {                        
                        count,
                        edges {
                            cursor,
                            node {
                                field,
                                message
                            }
                        }
                    },
                    node {
                        id,
                        __id,
                        name,
                        value
                    },
                    edge {
                       cursor,
                        node {
                            id,
                            __id,
                            name,
                            value
                        }
                    },
                    deletedId,
                    api {
                        connection(sort: [value_asc, name_asc], filter: {value: 13}) {
                            count,
                            edges {
                                cursor,
                                node {
                                    id,
                                    __id,
                                    name,
                                    value
                                }                    
                            }
                        }
                    }
                }
            }            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                                    {
                                      ""data"": {
                                        ""call"": {
                                         ""errors"": {
                                            ""count"": 2,
                                            ""edges"": [
                                              {
                                                ""cursor"": 1,
                                                ""node"": {
                                                  ""field"": null,
                                                  ""message"": ""Update failed""
                                                }
                                              },
                                              {
                                                ""cursor"": 2,
                                                ""node"": {
                                                  ""field"": ""id"",
                                                  ""message"": ""Node not found""
                                                }
                                              }
                                            ]
                                          },
                                          ""node"": null,
                                          ""edge"": null,
                                          ""deletedId"": null,
                                          ""api"": {
                                            ""connection"": {
                                              ""count"": 0,
                                              ""edges"": []
                                            }
                                          }
                                        }
                                      }
                                    }
                                ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing node requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task NodeQueryTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d0"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d1"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d2"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             }
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine)
            {
                UseJsonRepack =
                                                 true
            };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var globalId =
                JArray.Parse("[{\"f\":\"connection\"," + "\"id\":\"67885ba0-b284-438f-8393-ee9a9eb299d1\"}]")
                    .PackGlobalId();

            var query = $@"
                {{
                    node(id: ""{globalId.Replace("\"", "\\\"")}"") 
                    {{
                    ...F0
                    }}
                }}

                fragment F0 on TestApi_TestObject 
                {{
                    _id,
                    id,
                    name,
                    value
                }}            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                     r.ThrowOnUnhandledException = true;
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var expectedResult = $@"
                            {{
                              ""data"": {{
                                ""node"": {{
                                  ""_id"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                  ""id"": ""{globalId.Replace("\"", "\\\"")}"",
                                  ""name"": ""1-test"",
                                  ""value"": 100.0
                                }}
                              }}
                            }}";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing node requests from <see cref="ApiDescription"/> for empty id
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task NodeQueryEmptyTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d0"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d1"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d2"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             }
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine)
            {
                UseJsonRepack =
                                                 true
            };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var globalId =
                new JObject { { "empty", Guid.NewGuid().ToString("N") } }.PackGlobalId();

            var query = $@"
                {{
                    node(id: ""{globalId.Replace("\"", "\\\"")}"") 
                    {{
                    id
                    }}
                }}
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                     r.ThrowOnUnhandledException = true;
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var expectedResult = $@"
                            {{
                              ""data"": {{
                                ""node"": null
                              }}
                            }}";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing node requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task NodeQueryForMethodTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d0"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d1"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             },
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d2"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             }
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine)
            {
                UseJsonRepack =
                                                 true
            };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var globalId =
                JArray.Parse(
                    "[{\"f\":\"connectionMethod\", \"a\": {\"key\": \"test\", \"obj\": {\"name\": \"hello\"}}, \"id\":\"67885ba0-b284-438f-8393-ee9a9eb299d1"
                    + "\"}]").PackGlobalId();

            var query = $@"
                {{
                    node(id: ""{globalId.Replace("\"", "\\\"")}"") 
                    {{
                    ...F0
                    }}
                }}

                fragment F0 on TestApi_TestObject 
                {{
                    _id,
                    id,
                    name,
                    value
                }}            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var expectedResult = $@"
                            {{
                              ""data"": {{
                                ""node"": {{
                                  ""_id"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                  ""id"": ""{globalId.Replace("\"", "\\\"")}"",
                                  ""name"": ""1-test"",
                                  ""value"": 100.0
                                }}
                              }}
                            }}";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing node requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>
        /// Async task
        /// </returns>
        [Fact]
        public async Task NodeQuerySimpleObjectTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d1"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             }
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine)
            {
                UseJsonRepack =
                                                 true
            };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });
            var globalId = JArray.Parse("[{\"f\":\"nestedSync\"}, {\"f\":\"this\"}]").PackGlobalId();
            Assert.NotNull(globalId);
            this.output.WriteLine(globalId);
            this.output.WriteLine("--------------");
            var query = $@"
                            {{
                                node(id: ""{globalId.Replace("\"", "\\\"")}"") 
                                {{
                                    __typename,
                                    ...F0
                                }}
                            }}
                            fragment F0 on TestApi_NestedProvider 
                            {{
                                syncScalarField
                            }}";
            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var expectedResult = @"
                                        {
                                          ""data"": {
                                            ""node"": {
                                              ""__typename"": ""TestApi_NestedProvider"",
                                              ""syncScalarField"": ""SyncScalarField""                    
                                            }
                                          }
                                        }";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing node requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task NodeQueryRootTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d1"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             }
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine)
            {
                UseJsonRepack = true
            };

            var secondProvider = new DirectProvider(new TestSecondProvider(), this.output.WriteLine) { UseJsonRepack = true };

            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider, secondProvider });
            var globalId = JArray.Parse("[]").PackGlobalId();
            Assert.NotNull(globalId);
            this.output.WriteLine(globalId);
            this.output.WriteLine("--------------");
            var query = $@"
                            {{
                                node(id: ""{globalId.Replace("\"", "\\\"")}"") 
                                {{
                                    __typename,
                                    ...F0
                                }}
                            }}
                            fragment F0 on TestApi_TestSecondApi
                            {{
                                syncScalarField,
                                secondApiName
                            }}";
            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var expectedResult = @"
                                        {
                                          ""data"": {
                                            ""node"": {
                                              ""__typename"": ""TestApi_TestSecondApi"",
                                              ""syncScalarField"": ""SyncScalarField"",
                                              ""secondApiName"" : ""SecondApiName""
                                            }
                                          }
                                        }";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing nested node requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task NodeNestedQueryTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d1"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             }
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine)
            {
                UseJsonRepack =
                                                 true
            };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var globalId = JArray.Parse("[{\"f\":\"connection\","
                + "\"id\":\"67885ba0-b284-438f-8393-ee9a9eb299d1\"}]").PackGlobalId();

            var query = $@"
                {{
                    api {{
                        
                        node(id: ""{globalId.Replace("\"", "\\\"")}"") 
                        {{
                            ...F0
                        }}
                    }}
                }}

                fragment F0 on TestApi_TestObject 
                {{
                    _id,
                    id,
                    name,
                    value
                }}            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var expectedResult = $@"
                            {{
                              ""data"": {{
                                ""api"": {{
                                    ""node"": {{
                                      ""_id"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                      ""id"": ""{globalId.Replace("\"", "\\\"")}"",
                                      ""name"": ""1-test"",
                                      ""value"": 100.0
                                    }}
                                }}
                              }}
                            }}";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing simple fields requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task NodeQueryWithVariableTest()
        {
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "67885ba0-b284-438f-8393-ee9a9eb299d1"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             }
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            query ($id: ID){ 
                node(id: $id) {
                    ...F0
                }            
            }

            fragment F0 on TestApi_TestObject {
                _id,
                id,
                name,
                value
            }            
            ";

            var globalId = JArray.Parse("[{\"f\":\"connection\","
                + "\"id\":\"67885ba0-b284-438f-8393-ee9a9eb299d1\"}]")
                .PackGlobalId();

            var variables = $@"
            {{
                ""id"": ""{globalId.Replace("\"", "\\\"")}""
            }}
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                     r.Variables = new GraphQLSerializer(false).Deserialize<Inputs>(variables);
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var expectedResult = $@"
                            {{
                              ""data"": {{
                                ""node"": {{
                                  ""_id"": ""67885ba0-b284-438f-8393-ee9a9eb299d1"",
                                  ""id"": ""{globalId.Replace("\"", "\\\"")}"",
                                  ""name"": ""1-test"",
                                  ""value"": 100.0
                                }}
                              }}
                            }}";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing simple fields requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task MethodsRequestTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            {                
                api {
                    syncScalarMethod(intArg: 1, stringArg: ""test"", objArg: { syncScalarField: ""nested test"" }),
                    asyncObjectMethod(intArg: 1, stringArg: ""test"", objArg: { syncScalarField: ""nested test"" }, intArrayArg: [7, 8, 9]) {
                        syncScalarField,
                        asyncScalarField
                    },
                }
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""api"": {
                              ""syncScalarMethod"": ""ok"",
                              ""asyncObjectMethod"": {
                                ""syncScalarField"": ""returned type"",
                                ""asyncScalarField"": ""AsyncScalarField""
                              }
                            }
                          }
                        }";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing simple fields requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task VariablesRequestTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });
            var query = @"
            query Request($intArg: Int, $stringArg: String, $intArrayArg: [Int], $objArg: TestApi_NestedProvider_Input){                
                api {
                    syncScalarMethod(intArg: $intArg, stringArg: $stringArg, objArg: $objArg),
                    asyncObjectMethod(intArg: $intArg, stringArg: $stringArg, intArrayArg: $intArrayArg, objArg: $objArg) {
                        syncScalarField,
                        asyncScalarField
                    },
                }
            }
            ";

            var variables = @"
            {
                ""intArg"": 1,
                ""stringArg"": ""test"",
                ""objArg"": { ""syncScalarField"": ""nested test"" },
                ""intArrayArg"": [7, 8, 9]
            }
            ";

            var writer = new GraphQLSerializer(false);
            var inputs = writer.Deserialize<Inputs>(variables);

            Action<ExecutionOptions> configure = r =>
                {
                    r.Schema = schema;
                    r.Query = query;
                    r.Variables = inputs;
                    r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                };

            var result = await new DocumentExecuter().ExecuteAsync(
                             configure).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""api"": {
                              ""syncScalarMethod"": ""ok"",
                              ""asyncObjectMethod"": {
                                ""syncScalarField"": ""returned type"",
                                ""asyncScalarField"": ""AsyncScalarField""
                              }
                            }
                          }
                        }";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing call of simple mutation
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task MutationSimpleRequestTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"                          
            mutation M {
                    call: testApi_nestedAsync_setName(input: {name: ""hello world"", clientMutationId: ""test client id""}) {
                        result {
                            name
                        },
                        clientMutationId,
                        api {
                            syncScalarField
                        }
                    }
            }            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                     r.ThrowOnUnhandledException = true;
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""call"": {
                                ""result"": {
                                    ""name"": ""hello world""
                                },
                                ""clientMutationId"": ""test client id"",
                                ""api"": {
                                    ""syncScalarField"": ""SyncScalarField""
                                }
                            }
                          }
                        }";

            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing call of simple mutation
        /// </summary>
        /// <param name="input">The mutation input</param>
        /// <param name="name">The expected returned test object name</param>
        /// <param name="type">The expected returned test object type</param>
        /// <param name="value">The expected returned test object value</param>
        /// <param name="hasRecursive">A value indicating whether to expect nested object</param>
        /// <param name="recursiveName">The expected returned nested object name</param>
        /// <param name="recursiveType">The expected returned nested object type</param>
        /// <param name="recursiveValue">The expected returned nested object value</param>
        /// <param name="recursionArrayElementsCount">The expected returned nested objects array length</param>
        /// <returns>Async task</returns>
        [Theory]
        [InlineData("{ name: \"destination\" }", "destination", "Good", 1, true, "NestedSourceName", "Good", 10, 1)]
        [InlineData("{ recursion: { type: Bad } }", "SourceName", "Good", 1, true, "NestedSourceName", "Bad", 10, 1)]
        [InlineData("{ recursion: { value: 20 } }", "SourceName", "Good", 1, true, "NestedSourceName", "Good", 20, 1)]
        [InlineData("{ recursion: { recursion: { name: \"recursion\" } } }", "SourceName", "Good", 1, true, "NestedSourceName", "Good", 10, 1)]
        [InlineData("{ recursionArray: [] }", "SourceName", "Good", 1, true, "NestedSourceName", "Good", 10, 0)]
        public async Task MutationObjectUpdaterTest(
            string input,
            string name,
            string type,
            decimal value,
            bool hasRecursive,
            string recursiveName,
            string recursiveType,
            decimal recursiveValue,
            int recursionArrayElementsCount)
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = $@"                           
            mutation M {{
                    call: testApi_nestedAsync_setObject(input: {{ input:  {input}}}) {{
                        result {{
                            name,
                            type,
                            value,
                            recursion {{
                                name,
                                type,
                                value,
                                recursion {{
                                    name,
                                    type,
                                    value,
                                }}
                            }},
                            recursionArray {{
                                count,
                                edges {{
                                    node {{
                                        name,
                                        type,
                                        value
                                    }}
                                }}
                            }}
                        }}
                    }}
            }}            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                     r.ThrowOnUnhandledException = true;
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            var json = JObject.Parse(response);
            Assert.Equal(name, json.SelectToken("data.call.result.name")?.Value<string>());
            Assert.Equal(type, json.SelectToken("data.call.result.type")?.Value<string>());
            Assert.Equal(value, json.SelectToken("data.call.result.value")?.Value<decimal>());
            if (hasRecursive)
            {
                Assert.Equal(recursiveName, json.SelectToken("data.call.result.recursion.name")?.Value<string>());
                Assert.Equal(recursiveType, json.SelectToken("data.call.result.recursion.type")?.Value<string>());
                Assert.Equal(recursiveValue, json.SelectToken("data.call.result.recursion.value")?.Value<decimal>());
            }
            else
            {
                Assert.Equal(JValue.CreateNull(), json.SelectToken("data.call.result.recursion"));
            }

            Assert.Equal(recursionArrayElementsCount, json.SelectToken("data.call.result.recursionArray.count")?.Value<int>());
        }

        /// <summary>
        /// Testing call of simple mutation
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task MutationTriggerRequestTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"                          
            mutation M {
                    call: testApi_boolMutation {
                        result
                    }
            }            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""call"": {
                                ""result"": true                               
                            }
                          }
                        }";

            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing call of simple mutation
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task MutationInConnectionRequestTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"                          
            mutation M {
                    call: testApi_connection_untypedMutation(input: {uid: ""2ECCD307-8BF5-464D-97B3-D56A9944C2E0""}) {
                        result,
                        api {
                           syncScalarField
                        }
                    }
            }            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""call"": {
                                ""result"": false,
                                ""api"": {
                                    ""syncScalarField"": ""SyncScalarField""
                                }
                            }
                          }
                        }";

            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing correct schema generation from generated <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task SchemaGenerationTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };

            var secondProvider = new DirectProvider(new TestSecondProvider(), this.output.WriteLine) { UseJsonRepack = true };

            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider, secondProvider });

            var errors = SchemaGenerator.CheckSchema(schema).Select(e => $"Schema type error: {e}")
                .Union(SchemaGenerator.CheckSchemaIntrospection(schema)).Select(e => $"Schema introspection error: {e}");

            var hasErrors = false;
            foreach (var error in errors)
            {
                hasErrors = true;
                this.output.WriteLine(error);
            }


            var description = schema.Print();
            this.output.WriteLine("-------- Schema -----------");
            this.output.WriteLine(description);
            Assert.False(string.IsNullOrWhiteSpace(description));


            Assert.NotNull(schema.Query);
            Assert.Equal(3, schema.Query.Fields.Count());
            Assert.True(schema.Query.HasField("api"));

            var query = BaseInstaller.ReadTextResource(
                this.GetType().GetTypeInfo().Assembly,
                "KlusterKite.Web.Tests.GraphQL.Resources.IntrospectionQuery.txt");

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            Assert.False(hasErrors);
        }

        /// <summary>
        /// Testing simple fields requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task SimpleFieldsRequestTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            {                
                api {
                    asyncArrayOfScalarField,
                    asyncForwardedScalar,
                    nestedAsync {
                        asyncScalarField,
                        syncScalarField                        
                    },
                    asyncScalarField,
                    faultedSyncField,
                    forwardedArray,
                    syncArrayOfScalarField,
                    nestedSync {
                        asyncScalarField,
                        syncScalarField  
                    },
                    syncScalarField,
                    faultedASyncMethod {
                        asyncScalarField,
                        syncScalarField 
                    },
                    syncEnumField,
                    syncFlagsField
                }
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""api"": {
                              ""asyncArrayOfScalarField"": [
                                4.0,
                                5.0
                              ],
                              ""asyncForwardedScalar"": ""AsyncForwardedScalar"",
                              ""nestedAsync"": {
                                ""asyncScalarField"": ""AsyncScalarField"",
                                ""syncScalarField"": ""SyncScalarField""
                              },
                              ""asyncScalarField"": ""AsyncScalarField"",
                              ""faultedSyncField"": null,
                              ""forwardedArray"": [
                                5,
                                6,
                                7
                              ],
                              ""syncArrayOfScalarField"": [
                                1,
                                2,
                                3
                              ],
                              ""nestedSync"": {
                                ""asyncScalarField"": ""AsyncScalarField"",
                                ""syncScalarField"": ""SyncScalarField""
                              },
                              ""syncScalarField"": ""SyncScalarField"",
                              ""faultedASyncMethod"": null,
                              ""syncEnumField"": ""EnumItem1"",
                              ""syncFlagsField"": 1,
                            }
                          }
                        }
                        ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing simple fields requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <param name="fieldName">The requested api field name</param>
        /// <param name="expectingResult">A value indicating whether to expect data result</param>
        /// <param name="setSession">A value indicating whether to set authentication session</param>
        /// <param name="setUser">A value indicating whether to set authenticated user</param>
        /// <param name="setClientPrivilege">A value indicating whether to set "access" to client privileges scope</param>
        /// <param name="setUserPrivilege">A value indicating whether to set "access" to user privileges scope</param>
        /// <returns>Async task</returns>
        [Theory]
        [InlineData("requireSessionField", false, false, false, false, false)]
        [InlineData("requireSessionField", true, true, false, false, false)]
        [InlineData("requireUserField", true, false, false, false, false)]
        [InlineData("requireUserField", false, true, false, false, false)]
        [InlineData("requireUserField", true, true, true, false, false)]

        [InlineData("requirePrivilegeAnyField", true, false, false, false, false)]
        [InlineData("requirePrivilegeAnyField", false, true, false, false, false)]
        [InlineData("requirePrivilegeAnyField", true, true, false, true, false)]
        [InlineData("requirePrivilegeAnyField", false, true, true, false, false)]
        [InlineData("requirePrivilegeAnyField", true, true, true, false, true)]

        [InlineData("requirePrivilegeBothField", true, false, false, false, false)]
        [InlineData("requirePrivilegeBothField", false, true, false, false, false)]
        [InlineData("requirePrivilegeBothField", false, true, false, true, false)]
        [InlineData("requirePrivilegeBothField", false, true, true, false, false)]
        [InlineData("requirePrivilegeBothField", false, true, true, false, true)]
        [InlineData("requirePrivilegeBothField", true, true, true, true, true)]

        [InlineData("requirePrivilegeUserField", true, false, false, false, false)]
        [InlineData("requirePrivilegeUserField", false, true, false, false, false)]
        [InlineData("requirePrivilegeUserField", false, true, false, true, false)]
        [InlineData("requirePrivilegeUserField", false, true, true, false, false)]
        [InlineData("requirePrivilegeUserField", true, true, true, false, true)]

        [InlineData("requirePrivilegeClientField", true, false, false, false, false)]
        [InlineData("requirePrivilegeClientField", false, true, false, false, false)]
        [InlineData("requirePrivilegeClientField", true, true, false, true, false)]
        [InlineData("requirePrivilegeClientField", false, true, true, false, false)]
        [InlineData("requirePrivilegeClientField", false, true, true, false, true)]

        [InlineData("requirePrivilegeIgnoreOnUserPresentField", true, false, false, false, false)]
        [InlineData("requirePrivilegeIgnoreOnUserPresentField", false, true, false, false, false)]
        [InlineData("requirePrivilegeIgnoreOnUserPresentField", true, true, false, true, false)]
        [InlineData("requirePrivilegeIgnoreOnUserPresentField", true, true, true, false, false)]

        [InlineData("requirePrivilegeIgnoreOnUserNotPresentField", true, false, false, false, false)]
        [InlineData("requirePrivilegeIgnoreOnUserNotPresentField", true, true, false, false, false)]
        [InlineData("requirePrivilegeIgnoreOnUserNotPresentField", true, true, true, false, true)]
        [InlineData("requirePrivilegeIgnoreOnUserNotPresentField", false, true, true, false, false)]
        public async Task AuthorizationFieldTest(
            string fieldName,
            bool expectingResult,
            bool setSession,
            bool setUser,
            bool setClientPrivilege,
            bool setUserPrivilege)
        {
            var sink = CreateSecurityLogger();
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine)
            {
                UseJsonRepack = true
            };

            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });
            var query = $@"
            {{                
                api {{
                    {fieldName}
                }}
            }}
            ";

            var context = new RequestContext();
            if (setSession)
            {
                TestUser user = null;
                if (setUser)
                {
                    user = new TestUser { UserId = Guid.Empty.ToString("N") };
                }

                context.Authentication = new AccessTicket(
                    user,
                    setUserPrivilege ? new[] { "allow" } : new string[0],
                    "test",
                    "test",
                    setClientPrivilege ? new[] { "allow" } : new string[0],
                    DateTimeOffset.Now,
                    null,
                    null);
            }

            var result = await new DocumentExecuter().ExecuteAsync(
                r =>
                {
                    r.Schema = schema;
                    r.Query = query;
                    r.UserContext = context.ToExecutionOptionsUserContext();
                }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = $@"
                        {{
                          ""data"": {{
                            ""api"": {{ 
                                ""{fieldName}"": {(expectingResult ? "\"success\"" : "null")}
                            }}
                          }}
                        }}
                        ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
            Assert.Equal(expectingResult ? 0 : 1, sink.LogEvents.Count);
        }

        /// <summary>
        /// Testing the authorization to untyped mutation
        /// </summary>
        /// <param name="expectingResult">A value indicating whether to expect data result</param>
        /// <param name="setSession">A value indicating whether to set authentication session</param>
        /// <returns>The async task</returns>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public async Task AuthorizationMutationTest(bool expectingResult, bool setSession)
        {
            var sink = CreateSecurityLogger();
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine)
            {
                UseJsonRepack =
                                                 true
            };

            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });
            var query = @"
            mutation M {                
                testApi_authorizedMutation(input: {clientMutationId: ""test""}) {
                    result,
                    clientMutationId,
                    api {
                        syncScalarField
                    }
                }
            }
            ";

            var context = new RequestContext();
            if (setSession)
            {
                context.Authentication = new AccessTicket(
                    null,
                    new string[0],
                    "test",
                    "test",
                    new[] { "allow" },
                    DateTimeOffset.Now,
                    null,
                    null);
            }

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = context.ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = expectingResult
                      ? @"
                        {
                            ""data"": {
                                ""testApi_authorizedMutation"": {
                                    ""result"": ""ok"",
                                    ""clientMutationId"": ""test"",
                                    ""api"": { 
                                        ""syncScalarField"": ""SyncScalarField""
                                    } 
                                }
                            }
                        }
                        "
                      : @"
                        {
                            ""data"": {
                                ""testApi_authorizedMutation"": {
                                    ""result"": null,
                                    ""clientMutationId"": ""test"",
                                    ""api"": { 
                                        ""syncScalarField"": ""SyncScalarField""
                                    } 
                                }
                            }
                        }
                        ";

            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
            Assert.Equal(expectingResult ? 0 : 1, sink.LogEvents.Count);
        }

        /// <summary>
        /// Testing simple fields requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task FieldAccessLogTest()
        {
            var sink = CreateSecurityLogger();
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine)
            {
                UseJsonRepack =
                                                 true
            };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            {                
                api {
                    loggedNoMessageField,
                    loggedWithMessageField,
                    loggedConnection {
                        count
                    }
                }
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""api"": {
                               ""loggedNoMessageField"": ""success"",
                                ""loggedWithMessageField"": ""success"",
                                ""loggedConnection"": {
                                    ""count"": 0
                                }
                          }
                        }
                        ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));

            var events = sink.LogEvents.OrderBy(e => e.MessageTemplate.Text);
            foreach (var logEvent in events)
            {
                this.output.WriteLine(logEvent.RenderMessage());
            }

            Assert.Equal(3, sink.LogEvents.Count);
            Assert.Contains(events, e => e.RenderMessage() == "Connection queried");
            Assert.Contains(events, e => e.RenderMessage() == "LoggedWithMessageField accessed");
            Assert.Contains(events, e =>
                        e.RenderMessage()
                        == "The property LoggedNoMessageField of KlusterKite.API.Tests.Mock.TestProvider with id null was accessed");
        }

        /// <summary>
        /// Testing simple fields requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task MutationLogTest()
        {
            var sink = CreateSecurityLogger();
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine)
            {
                UseJsonRepack =
                                                 true
            };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            mutation {                
                untyped: testApi_loggedMutation {
                    result
                },
                create: testApi_loggedConnection_create(input: { newNode: {id: ""251FEEA8-D3AC-461D-A385-0CF2BA7A74E8"", name: ""hello world""} }) {
                    node {
                        name
                    }
                },
                update: testApi_loggedConnection_update(input: {id: ""251FEEA8-D3AC-461D-A385-0CF2BA7A74E8"", newNode: { name: ""updated world""} }) {
                    node {
                        name
                    }
                },
                delete: testApi_loggedConnection_delete(input: {id: ""251FEEA8-D3AC-461D-A385-0CF2BA7A74E8"" }) {
                    node {
                        name
                    }
                },
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                     r.ThrowOnUnhandledException = true;
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""untyped"": {
                                ""result"": ""ok""
                            },
                            ""create"": {
                                ""node"": {
                                    ""name"": ""hello world""
                                }
                            },
                            ""update"": {
                                ""node"": {
                                    ""name"": ""updated world""
                                }
                            },
                            ""delete"": {
                                ""node"": {
                                    ""name"": ""updated world""
                                }
                            },
                          }
                        }
                        ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));

            var events = sink.LogEvents;
            foreach (var logEvent in events)
            {
                this.output.WriteLine(logEvent.RenderMessage());
            }

            Assert.Equal(4, sink.LogEvents.Count);
            Assert.Equal(
                "The property LoggedMutation of KlusterKite.API.Tests.Mock.TestProvider with id null was accessed",
                sink.LogEvents[0].RenderMessage());
            Assert.Equal(
                "Connection created",
                sink.LogEvents[1].RenderMessage());
            Assert.Equal(
                "Connection updated",
                sink.LogEvents[2].RenderMessage());
            Assert.Equal(
                "Connection deleted",
                sink.LogEvents[3].RenderMessage());
        }

        /// <summary>
        /// Testing authorized connection query request from <see cref="ApiDescription"/>
        /// </summary>
        /// <param name="expectingResult">A value indicating whether to expect data result</param>
        /// <param name="setSession">A value indicating whether to set authentication session</param>
        /// /// <param name="privilege">The privilege to set</param>
        /// <returns>Async task</returns>
        [Theory]
        [InlineData(false, false, null)]
        [InlineData(false, true, "create")]
        [InlineData(true, true, "read")]
        public async Task AuthorizationConnectionQueryTest(bool expectingResult, bool setSession, string privilege)
        {
            var sink = CreateSecurityLogger();
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             }
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var context = new RequestContext();
            if (setSession)
            {
                context.Authentication = new AccessTicket(
                    null,
                    new string[0],
                    "test",
                    "test",
                    privilege != null ? new[] { privilege } : new string[0],
                    DateTimeOffset.Now,
                    null,
                    null);
            }

            var query = @"
            {                
                api {
                        authorizedConnection {
                            count,
                            edges {
                                node {
                                    name,
                                    value
                                }                    
                            }
                        }
                }                
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = context.ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = expectingResult
                            ? @"
                            {
                              ""data"": {
                                ""api"": {
                                  ""authorizedConnection"": {
                                    ""count"": 1,
                                    ""edges"": [
                                      {
                                        ""node"": {
                                          ""name"": ""1-test"",
                                          ""value"": 100.0
                                        }
                                      }
                                    ]
                                  }
                                }
                              }
                            }
                            "
                            : @"
                            {
                              ""data"": {
                                ""api"": {
                                  ""authorizedConnection"": null
                                }
                              }
                            }";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
            Assert.Equal(expectingResult ? 0 : 1, sink.LogEvents.Count);
        }

        /// <summary>
        /// Testing authorized connection mutation request from <see cref="ApiDescription"/>
        /// </summary>
        /// <param name="setSession">A value indicating whether to set authentication session</param>
        /// <param name="privilege">The privilege to set</param>
        /// <param name="mutationName">The mutation name</param>
        /// <param name="arguments">The mutation arguments</param>
        /// <param name="expectedResult">The expected result</param>
        /// <param name="expectingResult">A value indicating whether to expect data result</param>
        /// <returns>Async task</returns>
        [Theory]
        [InlineData(false, null, "authorizedConnection_create", "input: {newNode: {id: \"E2EB0672-9717-4F42-91BF-5A3893C591C3\", name: \"new node\"}}", "{\"data\": {\"m\": {\"node\": null}}}", false)]
        [InlineData(true, "query", "authorizedConnection_create", "input: {newNode: {id: \"E2EB0672-9717-4F42-91BF-5A3893C591C3\", name: \"new node\"}}", "{\"data\": {\"m\":  {\"node\": null}}}", false)]
        [InlineData(true, "create", "authorizedConnection_create", "input: {newNode: {id: \"E2EB0672-9717-4F42-91BF-5A3893C591C3\", name: \"new node\"}}", "{\"data\": {\"m\": {\"node\": {\"name\": \"new node\"}}}}", true)]

        [InlineData(false, null, "authorizedConnection_update", "input: {id: \"3BEEE369-11DF-4A30-BF11-1D8465C87110\", newNode: {name: \"new node\"}}", "{\"data\": {\"m\":  {\"node\": null}}}", false)]
        [InlineData(true, "query", "authorizedConnection_update", "input: {id: \"3BEEE369-11DF-4A30-BF11-1D8465C87110\", newNode: {name: \"new node\"}}", "{\"data\": {\"m\":  {\"node\": null}}}", false)]
        [InlineData(true, "update", "authorizedConnection_update", "input: {id: \"3BEEE369-11DF-4A30-BF11-1D8465C87110\", newNode: {name: \"new node\"}}", "{\"data\": {\"m\": {\"node\": {\"name\": \"new node\"}}}}", true)]

        [InlineData(false, null, "authorizedConnection_delete", "input: {id: \"3BEEE369-11DF-4A30-BF11-1D8465C87110\"}", "{\"data\": {\"m\":  {\"node\": null}}}", false)]
        [InlineData(true, "query", "authorizedConnection_delete", "input: {id: \"3BEEE369-11DF-4A30-BF11-1D8465C87110\"}", "{\"data\": {\"m\":  {\"node\": null}}}", false)]
        [InlineData(true, "delete", "authorizedConnection_delete", "input: {id: \"3BEEE369-11DF-4A30-BF11-1D8465C87110\"}", "{\"data\": {\"m\": {\"node\": {\"name\": \"1-test\"}}}}", true)]

        [InlineData(false, null, "authorizedNamedConnection_create", "input: {newNode: {id: \"E2EB0672-9717-4F42-91BF-5A3893C591C3\", name: \"new node\"}}", "{\"data\": {\"m\": {\"node\": null}}}", false)]
        [InlineData(true, "allow", "authorizedNamedConnection_create", "input: {newNode: {id: \"E2EB0672-9717-4F42-91BF-5A3893C591C3\", name: \"new node\"}}", "{\"data\": {\"m\":  {\"node\": null}}}", false)]
        [InlineData(true, "allow.Create", "authorizedNamedConnection_create", "input: {newNode: {id: \"E2EB0672-9717-4F42-91BF-5A3893C591C3\", name: \"new node\"}}", "{\"data\": {\"m\": {\"node\": {\"name\": \"new node\"}}}}", true)]

        [InlineData(false, null, "authorizedNamedConnection_update", "input: {id: \"3BEEE369-11DF-4A30-BF11-1D8465C87110\", newNode: {name: \"new node\"}}", "{\"data\": {\"m\":  {\"node\": null}}}", false)]
        [InlineData(true, "allow", "authorizedNamedConnection_update", "input: {id: \"3BEEE369-11DF-4A30-BF11-1D8465C87110\", newNode: {name: \"new node\"}}", "{\"data\": {\"m\":  {\"node\": null}}}", false)]
        [InlineData(true, "allow.Update", "authorizedNamedConnection_update", "input: {id: \"3BEEE369-11DF-4A30-BF11-1D8465C87110\", newNode: {name: \"new node\"}}", "{\"data\": {\"m\": {\"node\": {\"name\": \"new node\"}}}}", true)]

        [InlineData(false, null, "authorizedNamedConnection_delete", "input: {id: \"3BEEE369-11DF-4A30-BF11-1D8465C87110\"}", "{\"data\": {\"m\":  {\"node\": null}}}", false)]
        [InlineData(true, "allow", "authorizedNamedConnection_delete", "input: {id: \"3BEEE369-11DF-4A30-BF11-1D8465C87110\"}", "{\"data\": {\"m\":  {\"node\": null}}}", false)]
        [InlineData(true, "allow.Delete", "authorizedNamedConnection_delete", "input: {id: \"3BEEE369-11DF-4A30-BF11-1D8465C87110\"}", "{\"data\": {\"m\": {\"node\": {\"name\": \"1-test\"}}}}", true)]
        public async Task AuthorizationConnectionMutationTest(
            bool setSession,
            string privilege,
            string mutationName,
            string arguments,
            string expectedResult,
            bool expectingResult)
        {
            var sink = CreateSecurityLogger();
            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject
                                             {
                                                 Id =
                                                     Guid.Parse(
                                                         "{3BEEE369-11DF-4A30-BF11-1D8465C87110}"),
                                                 Name = "1-test",
                                                 Value = 100m
                                             }
                                     };

            var internalApiProvider = new TestProvider(initialObjects);
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var context = new RequestContext();
            if (setSession)
            {
                context.Authentication = new AccessTicket(
                    null,
                    new string[0],
                    "test",
                    "test",
                    privilege != null ? new[] { privilege } : new string[0],
                    DateTimeOffset.Now,
                    null,
                    null);
            }

            var query = $@"
            mutation M {{        
                m: testApi_{mutationName}({arguments}) {{
                    node {{
                        name
                    }}
                }}
            }}
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = context.ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
            Assert.Equal(expectingResult ? 0 : 1, sink.LogEvents.Count);
        }

        /// <summary>
        /// Testing simple fields requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task DateTimeTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            {                
                api {
                    dateTimeField,
                    dateTimeOffsetField,
                    dateTimeMethod(date: ""1980-09-25T10:00:00Z""),
                    dateTimeOffsetMethod(date: ""1980-09-25T10:00:00Z"")
                }
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 r.ThrowOnUnhandledException = true;
                             }).ConfigureAwait(true);
          
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""api"": {
                              ""dateTimeField"": ""1980-09-25T10:00:00Z"",                             
                              ""dateTimeOffsetField"": ""1980-09-25T10:00:00Z"",                             
                              ""dateTimeMethod"": true,                             
                              ""dateTimeOffsetMethod"": true                           
                            }
                          }
                        }
                        ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing simple fields requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task ErrorHandlingTest()
        {
            var internalApiProvider = new TestProvider();
            var faultingProvider = new FaultingProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var successProvider = new DirectProvider(new AdditionalProvider(), this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { faultingProvider, successProvider });

            var faultingQuery = @"
            {                
                api {
                    syncScalarField,
                    helloWorld
                }
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = faultingQuery;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                                    {
                                      ""errors"": [
                                        {
                                          ""message"": ""Error trying to resolve field 'api'."",
                                          ""locations"": [
                                            {
                                              ""line"": 3,
                                              ""column"": 17
                                            }
                                          ],
                                          ""path"": [
                                            ""api""
                                          ],
                                          ""extensions"": {
                                            ""code"": """",
                                            ""codes"": [
                                              """"
                                            ]
                                          }
                                        }
                                      ],
                                      ""data"": {
                                        ""api"": null
                                      }
                                    }";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));

            var successQuery = @"
            {                
                api {                    
                    helloWorld
                }
            }
            ";

            result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = successQuery;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            expectedResult = @"
                        {
                          ""data"": {
                                ""api"": {
                                ""helloWorld"": ""Hello world""
                                }
                            }
                        }";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Checking the work of <see cref="DeclareFieldAttribute.Access"/>
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task FieldAccessTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });
            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = Queries.IntrospectionQuery;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            var response = JObject.Parse(new GraphQLSerializer(true).Serialize(result));
            Assert.NotNull(response);

            var queryType = response.SelectToken("$.data.__schema.types[?(@.name == 'TestApi_NestedProvider')]");
            Assert.NotNull(queryType);
            Assert.Null(queryType.SelectToken("fields[?(@.name == 'argumentField')]"));
            Assert.NotNull(queryType.SelectToken("fields[?(@.name == 'readOnlyField')]"));

            var inputType = response.SelectToken("$.data.__schema.types[?(@.name == 'TestApi_NestedProvider_Input')]");
            Assert.NotNull(inputType);
            Assert.NotNull(inputType.SelectToken("inputFields[?(@.name == 'argumentField')]"));
            Assert.Null(inputType.SelectToken("inputFields[?(@.name == 'readOnlyField')]"));
        }

        /// <summary>
        /// If API defines method that has no published parameters, the mutations declared in method result should be accessable
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task ObjectMethodAsFieldSubMutationTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"                          
            mutation M {
                    call: testApi_asyncObjectMethodAsField_setName(input: {name: ""hello world"", clientMutationId: ""test client id""}) {
                        result {
                            name
                        },
                        clientMutationId,
                        api {
                            syncScalarField
                        }
                    }
            }            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""call"": {
                                ""result"": {
                                    ""name"": ""hello world""
                                },
                                ""clientMutationId"": ""test client id"",
                                ""api"": {
                                    ""syncScalarField"": ""SyncScalarField""
                                }
                            }
                          }
                        }";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing simple fields requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact()]
        public async Task SimpleFieldsMergeWithRootTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            {                
                api {
                    asyncArrayOfScalarField,
                    asyncForwardedScalar,
                    nestedAsync {
                        asyncScalarField
                    },
                    nestedAsync {
                        syncScalarField                        
                    },
                    asyncScalarField,
                    faultedSyncField,
                    forwardedArray,
                    syncArrayOfScalarField
                    
                }

                api {
                    nestedSync {
                        asyncScalarField,
                        syncScalarField  
                    },
                    syncScalarField,
                    faultedASyncMethod {
                        asyncScalarField,
                        syncScalarField 
                    },
                    syncEnumField,
                    syncFlagsField
                }
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""api"": {
                              ""asyncArrayOfScalarField"": [
                                4.0,
                                5.0
                              ],
                              ""asyncForwardedScalar"": ""AsyncForwardedScalar"",
                              ""nestedAsync"": {
                                ""asyncScalarField"": ""AsyncScalarField"",
                                ""syncScalarField"": ""SyncScalarField""
                              },
                              ""asyncScalarField"": ""AsyncScalarField"",
                              ""faultedSyncField"": null,
                              ""forwardedArray"": [
                                5,
                                6,
                                7
                              ],
                              ""syncArrayOfScalarField"": [
                                1,
                                2,
                                3
                              ],
                              ""nestedSync"": {
                                ""asyncScalarField"": ""AsyncScalarField"",
                                ""syncScalarField"": ""SyncScalarField""
                              },
                              ""syncScalarField"": ""SyncScalarField"",
                              ""faultedASyncMethod"": null,
                              ""syncEnumField"": ""EnumItem1"",
                              ""syncFlagsField"": 1,
                            }
                          }
                        }
                        ";

            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing simple fields requests from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task SimpleFieldsMergeTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var secondProvider = new DirectProvider(new TestSecondProvider(), this.output.WriteLine) { UseJsonRepack = true };

            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider, secondProvider });

            var query = @"
            {                
                api {
                    asyncArrayOfScalarField,
                    asyncForwardedScalar,
                    nestedAsync {
                        asyncScalarField
                    },
                    nestedAsync {
                        syncScalarField                        
                    },
                    asyncScalarField,
                    faultedSyncField,
                    forwardedArray,
                    syncArrayOfScalarField,
                    secondApiName                  
                }                
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""api"": {
                              ""asyncArrayOfScalarField"": [
                                4.0,
                                5.0
                              ],
                              ""asyncForwardedScalar"": ""AsyncForwardedScalar"",
                              ""nestedAsync"": {
                                ""asyncScalarField"": ""AsyncScalarField"",
                                ""syncScalarField"": ""SyncScalarField""
                              },
                              ""asyncScalarField"": ""AsyncScalarField"",
                              ""faultedSyncField"": null,
                              ""forwardedArray"": [
                                5,
                                6,
                                7
                              ],
                              ""syncArrayOfScalarField"": [
                                1,
                                2,
                                3
                              ],
                              ""secondApiName"": ""SecondApiName"",
                            }
                          }
                        }
                        ";

            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing querying the objects virtual id
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task VirtualIdTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var apiId = JArray.Parse("[]").PackGlobalId().Replace("\"", "\\\"");
            var nestedId = JArray.Parse("[{\"f\":\"nestedAsync\",\"id\":\"00000000-0000-0000-0000-000000000000\"}]").PackGlobalId().Replace("\"", "\\\"");
            var nested2Id = JArray.Parse("[{\"f\":\"nestedSync\",\"id\":\"00000000-0000-0000-0000-000000000000\"}]").PackGlobalId().Replace("\"", "\\\"");
            var nestedNestedId = JArray.Parse("[{\"f\":\"nestedAsync\",\"id\":\"00000000-0000-0000-0000-000000000000\"},{\"f\":\"this\",\"id\":\"00000000-0000-0000-0000-000000000000\"}]").PackGlobalId().Replace("\"", "\\\"");
            var nodeId = JArray.Parse("[{\"f\":\"arrayOfObjectNoIds\",\"id\":\"code1\"}]").PackGlobalId().Replace("\"", "\\\"");

            var query = @"
            {                
                api {
                    id
                    syncScalarField,
                    nestedAsync {
                        id,
                        syncScalarField,
                        this {
                            id,
                            syncScalarField
                        }
                    },
                    nestedSync {
                        id
                    }
                    arrayOfObjectNoIds(limit: 1) {
                        edges {
                            node {
                                id,
                                code
                            }
                        }
                    }
                }                
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 r.ThrowOnUnhandledException = true;
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = $@"
                                {{
                                    ""data"": {{
                                        ""api"": {{
                                            ""id"": ""{apiId}"", 
                                            ""syncScalarField"": ""SyncScalarField"",                             
                                            ""nestedAsync"": {{
                                                ""id"": ""{nestedId}"",
                                                ""syncScalarField"": ""SyncScalarField"",
                                                ""this"": {{
                                                    ""id"": ""{nestedNestedId}"",
                                                    ""syncScalarField"": ""SyncScalarField""
                                                }}
                                            }},
                                            ""nestedSync"": {{
                                                ""id"": ""{nested2Id}""
                                            }},
                                            ""arrayOfObjectNoIds"": {{
                                                ""edges"": [
                                                    {{
                                                        ""node"": {{
                                                            ""id"": ""{nodeId}"",
                                                            ""code"": ""code1""
                                                        }}
                                                    }}
                                                ]
                                            }}
                                        }}
                                    }}
                                }}
                        ";

            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing requests with use of fragments from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task RequestWithFragmentsTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            query FragmentsRequest {                
                api {
                   ...F
                }
            }

            fragment F on TestApi {
                    syncScalarField
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                     r.ThrowOnUnhandledException = true;
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""api"": {
                              ""syncScalarField"": ""SyncScalarField""
                            }
                          }
                        }
                        ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing requests with use of anonymous fragments from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task RequestWithFragmentsAnonymousTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            query FragmentsRequest {                
                api {
                   ...on TestApi {
                    syncScalarField
                    }
                }
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""api"": {
                              ""syncScalarField"": ""SyncScalarField""
                            }
                          }
                        }
                        ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing requests with use of anonymous fragments from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task RequestWithFragmentAsInterfaceAnonymousTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            query FragmentsRequest {                
                api {
                   ...on ITestApi {
                    syncScalarField
                    }
                }
            }
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = query;
                                 r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                             }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""api"": {
                              ""syncScalarField"": ""SyncScalarField""
                            }
                          }
                        }
                        ";
            Assert.Equal(CleanResponse(expectedResult), CleanResponse(response));
        }

        /// <summary>
        /// Testing requests with use of fragments from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task RecursiveFieldsTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            query FragmentsRequest {                
                api {
                   recursion {
                        recursion {
                            recursion {
                                syncScalarField
                            }
                        }
                   }
                }
            }
            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""api"": {
                                ""recursion"" : {
                                    ""recursion"" : {
                                        ""recursion"" : {
                                            ""syncScalarField"" : ""SyncScalarField""                               
                                        }
                                    }
                                }
                              }
                           }
                         }                        
                        ";

            Assert.Equal(
                CleanResponse(expectedResult),
                CleanResponse(response));
        }

        /// <summary>
        /// Testing requests with use of fragments from <see cref="ApiDescription"/>
        /// </summary>
        /// <returns>Async task</returns>
        [Fact]
        public async Task RequestWithAliasesTest()
        {
            var internalApiProvider = new TestProvider();
            var publishingProvider = new DirectProvider(internalApiProvider, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { publishingProvider });

            var query = @"
            query FragmentsRequest {                
                api {
                   test1: syncScalarField,
                   test2: syncScalarField,
                   nestedSync {
                        test3: syncScalarField,
                        test4: syncScalarField
                   }
                }
            }
            
            ";

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = query;
                                     r.UserContext = new RequestContext().ToExecutionOptionsUserContext();
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResult = @"
                        {
                          ""data"": {
                            ""api"": {
                              ""test1"": ""SyncScalarField"",
                              ""test2"": ""SyncScalarField"",
                              ""nestedSync"": {
                                  ""test3"": ""SyncScalarField"",
                                  ""test4"": ""SyncScalarField""
                              }
                            }
                          }
                        }
                        ";

            Assert.Equal(
                CleanResponse(expectedResult),
                CleanResponse(response));
        }

        /// <summary>
        /// Creates a virtual logger for security events
        /// </summary>
        /// <returns>The test sink</returns>
        private static ArraySink CreateSecurityLogger()
        {
            var loggerConfig = new LoggerConfiguration().MinimumLevel.Is(LogEventLevel.Verbose);
            var sink = new ArraySink();
            Func<LogEvent, bool> logFilter = log =>
            {
                LogEventPropertyValue value;
                return log.Properties.TryGetValue(Constants.LogRecordTypeKey, out value)
                       && (value as ScalarValue)?.Value is EnLogRecordType
                       && (EnLogRecordType)((ScalarValue)value).Value == EnLogRecordType.Security;
            };

            loggerConfig =
                loggerConfig.WriteTo.Logger(c => c.Filter.ByIncludingOnly(logFilter).WriteTo.Sink(sink, LogEventLevel.Verbose));
            Log.Logger = loggerConfig.CreateLogger();
            return sink;
        }

        /// <summary>
        /// An additional provider
        /// </summary>
        [ApiDescription(Name = "AdditionalApi")]
        public class AdditionalProvider : API.Provider.ApiProvider
        {
            /// <summary>
            /// Published string
            /// </summary>
            [DeclareField]
            [UsedImplicitly]
            public string HelloWorld => "Hello world";
        }

        /// <summary>
        /// The provider that throws an exception during resolve
        /// </summary>
        private class FaultingProvider : DirectProvider
        {
            /// <inheritdoc />
            public FaultingProvider(API.Provider.ApiProvider provider, Action<string> errorOutput)
                : base(provider, errorOutput)
            {
            }

            /// <inheritdoc />
            public override async ValueTask<JObject> GetData(List<ApiRequest> requests, RequestContext context)
            {
                await base.GetData(requests, context);
                throw new Exception("Test exception");
            }
        }

        /// <summary>
        /// The test user implementation for tests
        /// </summary>
        private class TestUser : IUser
        {
            /// <inheritdoc />
            public string UserId { get; set; }
        }

        /// <summary>
        /// The security log sink for tests
        /// </summary>
        private class ArraySink : ILogEventSink
        {
            /// <summary>
            /// Gets the list of logged events
            /// </summary>
            public List<LogEvent> LogEvents { get; } = new List<LogEvent>();

            /// <inheritdoc />
            public void Emit(LogEvent logEvent)
            {
                this.LogEvents.Add(logEvent);
            }
        }

    }

}