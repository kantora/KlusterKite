// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SchemeGeneratorTests.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Testing the <see cref="SchemaGenerator" />
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

    using KlusterKite.API.Client;
    using KlusterKite.Core;
    using KlusterKite.Security.Attributes;
    using KlusterKite.Web.GraphQL.Publisher;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Testing the <see cref="SchemaGenerator"/>
    /// </summary>
    public class SchemeGeneratorTests
    {
        /// <summary>
        /// The output.
        /// </summary>
        private readonly ITestOutputHelper output;

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemeGeneratorTests"/> class.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        public SchemeGeneratorTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Testing generator for some api with arrays
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task ArraysApiTest()
        {
            var viewerFields = new[]
                                   {
                                       ApiField.Scalar("id", EnScalarType.Guid, EnFieldFlags.Queryable | EnFieldFlags.IsKey),
                                       ApiField.Scalar("name", EnScalarType.String),
                                       ApiField.Scalar("numbers", EnScalarType.Integer, EnFieldFlags.IsArray | EnFieldFlags.Queryable)
                                   };
            var viewerType = new ApiObjectType("viewer", viewerFields);

            var objectFields = new[]
                                   {
                                       ApiField.Scalar("id", EnScalarType.Integer, EnFieldFlags.IsKey | EnFieldFlags.Queryable),
                                       ApiField.Scalar("name", EnScalarType.String)
                                   };

            var objectType = new ApiObjectType("object", objectFields);

            var api = new ApiDescription(
                "TestApi1",
                "0.0.0.1",
                new[] { viewerType, objectType },
                new[] { viewerType.CreateField("viewer"), objectType.CreateField("object", EnFieldFlags.IsConnection | EnFieldFlags.Queryable) });

            var provider = new MoqProvider
            {
                Description = api,
                Data = @"{
	                    ""viewer"": {
		                    ""__id"": ""FD73BAFB-3698-4FA1-81F5-27C8C83BB4F0"", 
		                    ""name"": ""test name"",
		                    ""numbers"": [1, 2, 3]
	                    }, 
	                    ""object"": {
		                    ""count"": 2, 
		                    ""edges"": [
			                    {""__id"": 10, ""node___id"": 10}, 
			                    {""__id"": 20, ""node___id"": 20}
		                    ]
	                    }
                    }"
            };

            var schema = SchemaGenerator.Generate(new List<ApiProvider> { provider });


            var description = schema.Print();
            this.output.WriteLine("-------- Schema -----------");
            this.output.WriteLine(description);
            Assert.False(string.IsNullOrWhiteSpace(description));
            

            Assert.NotNull(schema.Query);
            Assert.Equal(3, schema.Query.Fields.Count());
            Assert.True(schema.Query.HasField("api"));

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = @"
                                query {
                                    api {
                                        viewer {
                                            __id,
                                            name,
                                            numbers
                                        },
                                        object {
                                            count,
                                            edges {
                                                cursor,                                                
                                                node {
                                                    __id
                                                }
                                            }
                                        }
                                    }
                                }            
                                ";
                                 }).ConfigureAwait(true);

            this.output.WriteLine("-------- Response -----------");

            var response = new GraphQLSerializer(true).Serialize(result);
            this.output.WriteLine(response);

            var expectedResponse = @"{
                                      ""data"": {
                                        ""api"": {
                                          ""viewer"": {
                                            ""__id"": ""fd73bafb-3698-4fa1-81f5-27c8c83bb4f0"",
                                            ""name"": ""test name"",
		                                    ""numbers"": [1, 2, 3]
                                          },
                                          ""object"": {
                                            ""count"": 2,
                                            ""edges"": [
                                              {
                                                ""cursor"": 10,
                                                ""node"": {
                                                  ""__id"": 10
                                                }
                                              },
                                              {
                                                ""cursor"": 20,
                                                ""node"": {
                                                  ""__id"": 20
                                                }
                                              }
                                            ]
                                          }
                                        }
                                      }
                                    }";

            Assert.Equal(CleanResponse(expectedResponse), CleanResponse(response));
        }

        /// <summary>
        /// Testing generator for some api with enum
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task EnumApiTest()
        {
            var enumType = new ApiEnumType("EnumType", new[] { "item1", "item2" });

            var api = new ApiDescription(
                "TestApi1",
                "0.0.0.1",
                new ApiType[] { enumType },
                new[] { ApiField.Object("enumField", enumType.TypeName) });

            var provider = new MoqProvider { Description = api, Data = "{\"enumField\": \"item2\"}" };

            var schema = SchemaGenerator.Generate(new List<ApiProvider> { provider });

            var description = schema.Print();
            this.output.WriteLine("-------- Schema -----------");
            this.output.WriteLine(description);
            Assert.False(string.IsNullOrWhiteSpace(description));
            

            Assert.NotNull(schema.Query);
            Assert.Equal(3, schema.Query.Fields.Count());
            Assert.True(schema.Query.HasField("api"));

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = "query { api { enumField } } ";
                                 }).ConfigureAwait(true);

            this.output.WriteLine("-------- Response -----------");
            var response = new GraphQLSerializer(true).Serialize(result); 
            this.output.WriteLine(response);

            var expectedResponse = @"{
                                      ""data"": {
                                        ""api"": {
                                            ""enumField"": ""item2"" 
                                        }
                                     }                                      
                                    }";

            Assert.Equal(CleanResponse(expectedResponse), CleanResponse(response));
        }

        /// <summary>
        /// Testing generator for some api with boolean field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task BoolTest()
        {
            var api = new ApiDescription(
                "TestApi1",
                "0.0.0.1",
                new ApiType[] { },
                new[] { ApiField.Scalar("boolField", EnScalarType.Boolean) });

            var provider = new MoqProvider { Description = api, Data = "{\"boolField\": true}" };

            var schema = SchemaGenerator.Generate(new List<ApiProvider> { provider });

            var description = schema.Print();
            this.output.WriteLine("-------- Schema -----------");
            this.output.WriteLine(description);
            Assert.False(string.IsNullOrWhiteSpace(description));
            

            Assert.NotNull(schema.Query);
            Assert.Equal(3, schema.Query.Fields.Count());
            Assert.True(schema.Query.HasField("api"));

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                             {
                                 r.Schema = schema;
                                 r.Query = "query { api { boolField } } ";
                             }).ConfigureAwait(true);

            this.output.WriteLine("-------- Response -----------");
            var response = new GraphQLSerializer(true).Serialize(result); 
            this.output.WriteLine(response);

            var expectedResponse = @"{
                                      ""data"": {
                                        ""api"": {
                                            ""boolField"": true 
                                        }
                                     }                                      
                                    }";

            Assert.Equal(CleanResponse(expectedResponse), CleanResponse(response));
        }

        /// <summary>
        /// Testing generator for some simple single api
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task MultipleApiTest()
        {
            var viewerType1 = new ApiObjectType(
                "viewer1",
                new[] { ApiField.Scalar("id", EnScalarType.Integer), ApiField.Scalar("name", EnScalarType.String) });

            var viewerType2 = new ApiObjectType(
                "viewer2",
                new[] { ApiField.Scalar("description", EnScalarType.String) });

            var objectType1 = new ApiObjectType("object", new[] { ApiField.Scalar("id", EnScalarType.String) });
            var objectType2 = new ApiObjectType("object", new[] { ApiField.Scalar("id", EnScalarType.String) });

            var api1 = new ApiDescription(
                "TestApi1",
                "0.0.0.1",
                new[] { viewerType1, objectType1 },
                new[] { viewerType1.CreateField("viewer"), objectType1.CreateField("object1") });

            var api2 = new ApiDescription(
                "TestApi2",
                "0.0.0.1",
                new[] { viewerType2, objectType2 },
                new[] { viewerType2.CreateField("viewer"), objectType2.CreateField("object2") });

            var provider1 = new MoqProvider
                                {
                                    Description = api1,
                                    Data =
                                        "{\"viewer\": {\"__id\": 1, \"name\": \"test name\"}, \"object1\": {\"__id\": 10}}"
                                };

            var provider2 = new MoqProvider
                                {
                                    Description = api2,
                                    Data =
                                        "{\"viewer\": {\"description\": \"test description\"}, \"object2\": {\"__id\": 123}}"
                                };

            var schema = SchemaGenerator.Generate(new List<ApiProvider> { provider1, provider2 });


            var description = schema.Print();
            this.output.WriteLine("-------- Schema -----------");
            this.output.WriteLine(description);
            Assert.False(string.IsNullOrWhiteSpace(description));
            

            Assert.NotNull(schema.Query);
            Assert.Equal(3, schema.Query.Fields.Count());
            Assert.True(schema.Query.HasField("api"));

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = @"
                                query {
                                    api {
                                        viewer {
                                            __id,
                                            name,
                                            description
                                        },
                                        object1 {
                                            __id
                                        },
                                        object2 {
                                            __id
                                        }
                                    }
                                }            
                                ";
                                 }).ConfigureAwait(true);

            this.output.WriteLine("-------- Response -----------");
            var response = new GraphQLSerializer(true).Serialize(result); 
            this.output.WriteLine(response);
            var expectedResponse = @"{
                                      ""data"": {
                                        ""api"": {
                                          ""viewer"": {
                                            ""__id"": 1,
                                            ""name"": ""test name"",
                                            ""description"": ""test description""
                                          },
                                          ""object1"": {
                                            ""__id"": 10
                                          },
                                          ""object2"": {
                                            ""__id"": 123
                                          }
                                        }
                                      }
                                    }";
            Assert.Equal(CleanResponse(expectedResponse), CleanResponse(response));
        }

        /// <summary>
        /// Testing generator for some simple single api
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task NonEmptyApiTest()
        {
            var viewerType = new ApiObjectType(
                "viewer",
                new[] { ApiField.Scalar("id", EnScalarType.Integer), ApiField.Scalar("name", EnScalarType.String) });

            var api = new ApiDescription(
                "TestApi1",
                "0.0.0.1",
                new[] { viewerType },
                new[] { viewerType.CreateField("viewer") });

            var provider = new MoqProvider
                               {
                                   Description = api,
                                   Data = "{\"viewer\": {\"__id\": 1, \"name\": \"test name\"}}"
                               };

            var schema = SchemaGenerator.Generate(new List<ApiProvider> { provider });


            var description = schema.Print();
            this.output.WriteLine("-------- Schema -----------");
            this.output.WriteLine(description);
            Assert.False(string.IsNullOrWhiteSpace(description));
            

            Assert.NotNull(schema.Query);
            Assert.Equal(3, schema.Query.Fields.Count());
            Assert.True(schema.Query.HasField("api"));

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     r.Query = @"
                                query {
                                    api {
                                        viewer {
                                            __id,
                                            name
                                        }
                                    }
                                }            
                                ";
                                 }).ConfigureAwait(true);

            this.output.WriteLine("-------- Response -----------");
            var response = new GraphQLSerializer(true).Serialize(result); 
            this.output.WriteLine(response);
            var expectedResponse = @"{
                                      ""data"": {
                                        ""api"": {
                                          ""viewer"": {
                                            ""__id"": 1,
                                            ""name"": ""test name""
                                          }
                                        }
                                      }
                                    }";
            Assert.Equal(CleanResponse(expectedResponse), CleanResponse(response));
        }

        /// <summary>
        /// Tests introspection query and api descriptions
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task SchemaDescriptionTest()
        {
            var checkAttributeArguments = new[]
                                              {
                                                  ApiField.Scalar(
                                                      "attribute",
                                                      EnScalarType.String,
                                                      description: "attribute to check")
                                              };

            var objectFields = new[]
                                   {
                                       ApiField.Scalar(
                                           "uid",
                                           EnScalarType.Guid,
                                           description: "The object unique identifier"),
                                       ApiField.Scalar("name", EnScalarType.String, description: "The object name"),
                                       ApiField.Scalar(
                                           "attributes",
                                           EnScalarType.String,
                                           EnFieldFlags.IsArray,
                                           description: "The object attributes"),
                                       ApiField.Scalar(
                                           "checkAttribute",
                                           EnScalarType.Boolean,
                                           arguments: checkAttributeArguments,
                                           description: "checks the attribute")
                                   };

            var objectType = new ApiObjectType("object", objectFields) { Description = "Some abstract object" };
            var apiField = objectType.CreateField(
                "new",
                description: "The new object data");

            var mutations = new[]
                                {
                                    ApiMutation.CreateFromField(
                                        ApiField.Object(
                                            "objects_create",
                                            "object",
                                            arguments: new[] { apiField },
                                            description: "creates a new object"),
                                        ApiMutation.EnType.ConnectionCreate,
                                        new List<ApiRequest>())
                                };

            var api = new ApiDescription(
                          "TestApi",
                          "0.0.0.1",
                          new[] { objectType },
                          new[] { objectType.CreateField("objects", EnFieldFlags.IsConnection, "The objects dataset") },
                          mutations) { Description = "The test api" };

            var provider = new MoqProvider { Description = api };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { provider });

            var errors = SchemaGenerator.CheckSchema(schema).Select(e => $"Schema type error: {e}")
                .Union(SchemaGenerator.CheckSchemaIntrospection(schema))
                .Select(e => $"Schema introspection error: {e}");

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
            

            Assert.False(hasErrors);
            var query = BaseInstaller.ReadTextResource(
                this.GetType().GetTypeInfo().Assembly,
                "KlusterKite.Web.Tests.GraphQL.Resources.IntrospectionQuery.txt");

            var result = await new DocumentExecuter().ExecuteAsync(
                             r =>
                                 {
                                     r.Schema = schema;
                                     
                                     r.Query = query;
                                 }).ConfigureAwait(true);
            var response = new GraphQLSerializer(true).Serialize(result); 
            this.output.WriteLine(response);

            var expectedResponse = BaseInstaller.ReadTextResource(
                this.GetType().GetTypeInfo().Assembly,
                "KlusterKite.Web.Tests.GraphQL.Resources.SchemaDescriptionTestSnapshot.txt");

            Assert.Equal(CleanResponse(expectedResponse), CleanResponse(response));
        }

        /// <summary>
        /// Testing generator for some empty single api
        /// </summary>
        [Fact]
        public void Trivial2Test()
        {
            var api = new ApiDescription
                          {
                              ApiName = "TestApi1",
                              TypeName = "TestApi1",
                              Version = new Version("0.0.0.1"),
                          };

            var provider = new MoqProvider { Description = api };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { provider });

            var description = schema.Print();
            this.output.WriteLine(description);
            Assert.False(string.IsNullOrWhiteSpace(description));
            

            Assert.NotNull(schema.Query);
            Assert.Equal(3, schema.Query.Fields.Count());
            Assert.True(schema.Query.HasField("api"));
        }

        /// <summary>
        /// Testing trivial schema generator
        /// </summary>
        [Fact]
        public void TrivialTest()
        {
            var schema = SchemaGenerator.Generate(new List<ApiProvider>());


            var description = schema.Print();
            this.output.WriteLine(description);
            Assert.False(string.IsNullOrEmpty(description));
            
        }

        /// <summary>
        /// Removes formatting values from response json
        /// </summary>
        /// <param name="response">The json string</param>
        /// <returns>Cleaned json string</returns>
        private static string CleanResponse(string response)
        {
            return JsonConvert.DeserializeObject<JObject>(response).ToString(Formatting.None);
        }

        /// <summary>
        /// Impersonate api provider with preset data
        /// </summary>
        private class MoqProvider : ApiProvider
        {
            /// <summary>
            /// Gets or sets the api data
            /// </summary>
            public string Data { get; set; }

            /// <inheritdoc />
            public override ValueTask<JObject> GetData(List<ApiRequest> requests, RequestContext context)
            {
                return ValueTask.FromResult(JsonConvert.DeserializeObject<JObject>(this.Data));
            }
        }
    }
}