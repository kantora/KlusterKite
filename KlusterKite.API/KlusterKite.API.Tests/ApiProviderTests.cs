// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ApiProviderTests.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Testing the api provider
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.API.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using KlusterKite.API.Attributes;
    using KlusterKite.API.Client;
    using KlusterKite.API.Provider;
    using KlusterKite.API.Provider.Resolvers;
    using KlusterKite.API.Tests.Mock;
    using KlusterKite.Security.Attributes;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Testing the api provider
    /// </summary>
    public class ApiProviderTests
    {
        /// <summary>
        /// The output.
        /// </summary>
        private readonly ITestOutputHelper output;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiProviderTests"/> class.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        public ApiProviderTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Testing the correct description generation
        /// </summary>
        [Fact]
        public void DescriptionGenerationTest()
        {
            var provider = new TestApi();

            foreach (var e in provider.GenerationErrors)
            {
                this.output.WriteLine($"Generation error: {e}");
            }

            Assert.Empty(provider.GenerationErrors);

            var description = provider.ApiDescription;
            Assert.NotNull(description);

            var jsonDescription = JsonConvert.SerializeObject(
                description,
                Formatting.Indented,
                new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Error });
            this.output.WriteLine(jsonDescription);

            Assert.Equal("TestApi", description.ApiName);
            Assert.Equal(this.GetType().GetTypeInfo().Assembly.GetName().Version, description.Version);
            Assert.Equal(9, description.Types.Count);

            Assert.NotNull(description.Types.FirstOrDefault(t => t.TypeName.ToLower().Contains("EnTest".ToLower())));
            Assert.Null(description.Types.FirstOrDefault(t => t.TypeName.ToLower().Contains("EnFlags".ToLower())));

            var nodeType = description.Types.FirstOrDefault(t => t.TypeName.ToLower().Contains("nodeobject"));
            Assert.NotNull(nodeType);

            Assert.Contains(description.Fields, f => f.Name == "arrayedObjects");
            Assert.Equal(nodeType.TypeName, description.Fields.First(f => f.Name == "arrayedObjects").TypeName);
            Assert.Equal(EnFieldFlags.IsConnection | EnFieldFlags.Queryable, description.Fields.First(f => f.Name == "arrayedObjects").Flags);

            Assert.Contains(description.Fields, f => f.Name == "connectedObjects");
            Assert.Equal(nodeType.TypeName, description.Fields.First(f => f.Name == "connectedObjects").TypeName);
            Assert.Equal(EnFieldFlags.IsConnection | EnFieldFlags.Queryable, description.Fields.First(f => f.Name == "connectedObjects").Flags);
            Assert.Contains(description.Mutations, m => m.Name == "connectedObjects.create");
            //// Assert.Equal(nodeType.TypeName, description.Mutations.First(m => m.Name == "connectedObjects.create").TypeName);
            Assert.Equal(nodeType.TypeName, description.Mutations.First(m => m.Name == "connectedObjects.create").Arguments.First().TypeName);
            Assert.Contains(description.Mutations, m => m.Name == "connectedObjects.update");
            //// Assert.Equal(nodeType.TypeName, description.Mutations.First(m => m.Name == "connectedObjects.update").TypeName);
            Assert.Equal(EnScalarType.Guid, description.Mutations.First(m => m.Name == "connectedObjects.update").Arguments.First().ScalarType);
            Assert.Equal(nodeType.TypeName, description.Mutations.First(m => m.Name == "connectedObjects.update").Arguments.Skip(1).First().TypeName);
            Assert.Contains(description.Mutations, m => m.Name == "connectedObjects.delete");
            //// Assert.Equal(nodeType.TypeName, description.Mutations.First(m => m.Name == "connectedObjects.delete").TypeName);
            Assert.Equal(EnScalarType.Guid, description.Mutations.First(m => m.Name == "connectedObjects.delete").Arguments.First().ScalarType);

            Assert.Contains(description.Fields, f => f.Name == "publishedStringProperty");
            Assert.Equal(EnScalarType.String, description.Fields.First(f => f.Name == "publishedStringProperty").ScalarType);
            Assert.Equal(EnFieldFlags.IsFilterable | EnFieldFlags.IsSortable | EnFieldFlags.Queryable, description.Fields.First(f => f.Name == "publishedStringProperty").Flags);

            Assert.Contains(description.Fields, f => f.Name == "stringArray");
            Assert.Equal(EnScalarType.String, description.Fields.First(f => f.Name == "stringArray").ScalarType);
            Assert.Equal(EnFieldFlags.IsArray | EnFieldFlags.Queryable, description.Fields.First(f => f.Name == "stringArray").Flags);

            Assert.DoesNotContain(description.Fields, f => f.Name == "unPublishedStringProperty");

            Assert.Contains(description.Fields, f => f.Name == "publishedStringMethod");
            Assert.Equal(EnScalarType.String, description.Fields.First(f => f.Name == "publishedStringMethod").ScalarType);
            Assert.Equal(EnFieldFlags.Queryable, description.Fields.First(f => f.Name == "publishedStringMethod").Flags);
            Assert.Equal(2, description.Fields.First(f => f.Name == "publishedStringMethod").Arguments.Count);

            Assert.Contains(description.Fields, f => f.Name == "validateNode");
            Assert.Equal(EnScalarType.Boolean, description.Fields.First(f => f.Name == "validateNode").ScalarType);
            Assert.Equal(EnFieldFlags.Queryable, description.Fields.First(f => f.Name == "validateNode").Flags);
            Assert.Equal(2, description.Fields.First(f => f.Name == "validateNode").Arguments.Count);

            Assert.Contains(description.Mutations, m => m.Name == "mutateNode");
            Assert.Equal(nodeType.TypeName, description.Mutations.First(m => m.Name == "mutateNode").TypeName);
            Assert.Equal(nodeType.TypeName, description.Mutations.First(m => m.Name == "mutateNode").Arguments.First().TypeName);
        }

        /// <summary>
        /// Testing that <see cref="ApiEnumType"/> receives it's value descriptions
        /// </summary>
        [Fact]
        public void EnumDescriptionsTest()
        {
            var typeDescription = EnumResolver<TestObject.EnObjectType>.GeneratedType as ApiEnumType;
            Assert.NotNull(typeDescription);
            Assert.NotNull(typeDescription.Descriptions);
            Assert.True(typeDescription.Descriptions.ContainsKey(nameof(TestObject.EnObjectType.Good)));
            Assert.Equal("This is good object", typeDescription.Descriptions[nameof(TestObject.EnObjectType.Good)]);
        }

        /// <summary>
        /// The test node object
        /// </summary>
        public class NodeObject
        {
            /// <summary>
            /// Gets or sets the node object id
            /// </summary>
            [UsedImplicitly]
            [DeclareField(Description = "the object uid", IsKey = true)]
            public Guid Id { get; set; }

            /// <summary>
            /// Gets or sets the integer value
            /// </summary>
            [UsedImplicitly]
            [DeclareField(Description = "the integer property")]
            public int IntValue { get; set; }

            /// <summary>
            /// Gets or sets the integer value
            /// </summary>
            [UsedImplicitly]
            [DeclareField(Description = "the nullable integer property")]
            public int? IntNullableValue { get; set; }

            /// <summary>
            /// Gets the string array
            /// </summary>
            [UsedImplicitly]
            [DeclareField(Description = "the string array")]
            public string[] StringArray => new[] { "object test 1", "object test 2" };

            /// <summary>
            /// Published scalar method
            /// </summary>
            /// <param name="argument1">String argument number 1</param>
            /// <param name="argument2">String argument number 2</param>
            /// <returns>The test string</returns>
            [UsedImplicitly]
            [DeclareField(Description = "the scalar method")]
            public string PublishedStringMethod(string argument1, int argument2) => "Published string method";
        }

        /// <summary>
        /// The test non-node object
        /// </summary>
        [UsedImplicitly]
        [ApiDescription(Description = "Side non-node object")]
        public class NotNodeObject
        {
            /// <summary>
            /// Gets or sets the node object id
            /// </summary>
            [DeclareField]
            [UsedImplicitly]
            public Guid Id { get; set; }
        }

        /// <summary>
        /// Some test api
        /// </summary>
        [ApiDescription(Name = "TestApi", Description = "Test api containing all possible field / method combinations")]
        public class TestApi : ApiProvider
        {
            /// <summary>
            /// The node object connection
            /// </summary>
            private readonly NodeObjectConnection nodeObjectConnection = new NodeObjectConnection();
            
            /// <summary>
            /// Test enum
            /// </summary>
            [ApiDescription(Description = "The test enum")]
            public enum EnTest
            {
                /// <summary>
                /// Test enum item1
                /// </summary>
                EnumItem1,

                /// <summary>
                /// Test enum item2
                /// </summary>
                EnumItem2
            }

            /// <summary>
            /// Test enum
            /// </summary>
            [ApiDescription(Description = "The test enum")]
            [Flags]
            public enum EnFlags
            {
                /// <summary>
                /// Test enum item1
                /// </summary>
                EnumItem1 = 1,

                /// <summary>
                /// Test enum item2
                /// </summary>
                EnumItem2 = 2
            }

            /// <summary>
            /// Gets just the array of objects
            /// </summary>
            [UsedImplicitly]
            [DeclareField(Description = "An array of objects")]
            public Task<IEnumerable<NodeObject>> ArrayedObjects => Task.FromResult((IEnumerable<NodeObject>)new[] { new NodeObject(), new NodeObject() });

            /// <summary>
            /// Gets the list of connected objects
            /// </summary>
            [UsedImplicitly]
            [DeclareConnection(Description = "The connected objects", CanCreate = true, CanUpdate = true, CanDelete = true)]
            public INodeConnection<NodeObject> ConnectedObjects => this.nodeObjectConnection;

            /// <summary>
            /// The published string property.
            /// </summary>
            [UsedImplicitly]
            [DeclareField(Description = "Published string")]
            public string PublishedStringProperty => "Published string";

            /// <summary>
            /// The published string property.
            /// </summary>
            [UsedImplicitly]
            [DeclareField(Description = "Published enum")]
            public EnTest PublishedEnumProperty => EnTest.EnumItem1;

            /// <summary>
            /// The published string property.
            /// </summary>
            [UsedImplicitly]
            [DeclareField(Description = "Published flag")]
            public EnFlags PublishedFlagsProperty => EnFlags.EnumItem1 | EnFlags.EnumItem2;

            /// <summary>
            /// Gets the string array
            /// </summary>
            [UsedImplicitly]
            [DeclareField(Description = "Published string array")]
            public string[] StringArray => new[] { "test 1", "test 2" };

            /// <summary>
            /// The unpublished string property.
            /// </summary>
            [UsedImplicitly]
            public string UnPublishedStringProperty => "Unpublished string";

            /// <summary>
            /// Published scalar method
            /// </summary>
            /// <param name="argument1">String argument number 1</param>
            /// <param name="argument2">String argument number 2</param>
            /// <returns>The test string</returns>
            [UsedImplicitly]
            [DeclareField(Description = "Published string method")]
            public string PublishedStringMethod(
                [ApiDescription(Description = "first argument")] string argument1,
                [ApiDescription(Description = "second argument")] int argument2) => "Published string method";

            /// <summary>
            /// Published method with object and array arguments
            /// </summary>
            /// <param name="node">An object argument</param>
            /// <param name="properties">An array argument</param>
            /// <returns>The scalar value </returns>
            [UsedImplicitly]
            [DeclareField(Description = "Published method with complex arguments")]
            public bool ValidateNode(NodeObject node, List<string> properties)
            {
                return true;
            }

            /// <summary>
            /// Publishes a mutation
            /// </summary>
            /// <param name="node">The node value</param>
            /// <returns>The node result</returns>
            [DeclareMutation(Description = "Mutation declaration")]
            [UsedImplicitly]
            public Task<NodeObject> MutateNode([ApiDescription(Description = "mutating node")]NodeObject node)
            {
                return Task.FromResult(node);
            }

            /// <summary>
            /// The node object connection
            /// </summary>
            private class NodeObjectConnection : INodeConnection<NodeObject>
            {
                /// <summary>
                /// The initial nodes list
                /// </summary>
                private readonly List<NodeObject> nodes = new List<NodeObject> { new NodeObject(), new NodeObject() };

                /// <inheritdoc />
                public Task<NodeObject> GetById(Guid id)
                {
                    return Task.FromResult(this.nodes.FirstOrDefault(n => n.Id == id));
                }

                /// <inheritdoc />
                public Task<QueryResult<NodeObject>> Query(
                    Expression<Func<NodeObject, bool>> filter,
                    IEnumerable<SortingCondition> sort,
                    int? limit,
                    int? offset,
                    ApiRequest apiRequest)
                {
                    var query = this.nodes.AsQueryable();

                    if (filter != null)
                    {
                        query = query.Where(filter);
                    }

                    var count = query.Count();
                    if (sort != null)
                    {
                        query = query.ApplySorting(sort);
                    }

                    if (offset.HasValue)
                    {
                        query = query.Skip(offset.Value);
                    }

                    if (limit.HasValue)
                    {
                        query = query.Take(limit.Value);
                    }

                    return Task.FromResult(new QueryResult<NodeObject> { Count = count, Items = query.ToList() });
                }

                /// <inheritdoc />
                public Task<MutationResult<NodeObject>> Create(NodeObject newNode)
                {
                    newNode.Id = Guid.NewGuid();
                    this.nodes.Add(newNode);
                    return Task.FromResult(new MutationResult<NodeObject> { Result = newNode });
                }

                /// <inheritdoc />
                public Task<MutationResult<NodeObject>> Update(object id, NodeObject newNode, ApiRequest request)
                {
                    return Task.FromResult(new MutationResult<NodeObject> { Result = newNode });
                }

                /// <inheritdoc />
                public async Task<MutationResult<NodeObject>> Delete(object id)
                {
                    var oldNode = await this.GetById((Guid)id);
                    if (oldNode != null)
                    {
                        this.nodes.Remove(oldNode);
                    }

                    return new MutationResult<NodeObject> { Result = oldNode };
                }
            }
        }

        /// <summary>
        /// Just class to demonstrate api declaration
        /// </summary>
        [ApiDescription(Name = "DemoApi", Description = "Just class do demonstrate api declaration")]
        public class DemoApi : ApiProvider
        {
            /// <summary>
            /// The published scalar property.
            /// </summary>
            /// <remarks>
            /// The result will be transformed to <see cref="JValue"/>
            /// </remarks>
            [UsedImplicitly]
            [DeclareField(Description = "The published scalar property")]
            public string PublishedStringProperty => "Published string";

            /// <summary>
            /// Some nested api
            /// </summary>
            /// <remarks>
            /// The result will be resolved for each nested field / method
            /// </remarks>
            [UsedImplicitly]
            [DeclareField(Description = "The published nested api")]
            public TestApi Viewer => new TestApi();

            /// <summary>
            /// The regular api method
            /// </summary>
            /// <remarks>
            /// The result will be resolved for each nested field / method
            /// </remarks>
            /// <param name="nodeUid">
            /// The node Id.
            /// </param>
            /// <returns>Some object</returns>
            [UsedImplicitly]
            [DeclareField(Description = "The regular api method")]
            public NodeObject JustMethod([ApiDescription(Description = "The node uid")]Guid nodeUid) => new NodeObject { Id = nodeUid };

            /// <summary>
            /// The api method with context usage. Context argument is not published to api.
            /// </summary>
            /// <remarks>
            /// The result will be resolved for each nested field / method
            /// </remarks>
            /// <param name="context">
            /// The context.
            /// </param>
            /// <param name="nodeUid">
            /// The node Id.
            /// </param>
            /// <returns>
            /// Some object
            /// </returns>
            [UsedImplicitly]
            [DeclareField(Description = "The api method with context usage")]
            public NodeObject JustMethodWithContext(RequestContext context, [ApiDescription(Description = "The node uid")]Guid nodeUid) => new NodeObject { Id = nodeUid };

            /// <summary>
            /// The api method with sub-request parsing
            /// </summary>
            /// <param name="subRequest">
            /// The sub-request
            /// </param>
            /// <param name="nodeUid">
            /// The node Id.
            /// </param>
            /// <returns>
            /// Some object
            /// </returns>
            [UsedImplicitly]
            [DeclareField(Description = "The api method with sub-request parsing")]
            public NodeObject JustMethodWithSubRequest(ApiRequest subRequest, [ApiDescription(Description = "The node uid")]Guid nodeUid) => new NodeObject { Id = nodeUid };

            /// <summary>
            /// This method is considered as forwarding to some other api provider. T
            /// </summary>
            /// <remarks>
            /// The result will be forward back to requester with no further process
            /// </remarks>
            /// <param name="subRequest">
            /// The sub-request
            /// </param>
            /// <param name="nodeUid">
            /// The node Id.
            /// </param>
            /// <returns>
            /// Some object
            /// </returns>
            [UsedImplicitly]
            [DeclareField(Description = "The api method with context usage")]
            public JObject ApiResolveForward(ApiRequest subRequest, [ApiDescription(Description = "The node uid")]Guid nodeUid) => new JObject();
        }
    }
}