﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ApiProviderResolveTests.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Testing <see cref="ApiProvider" /> for resolving in various scenarios
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.API.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    using KlusterKite.API.Client;
    using KlusterKite.API.Provider;
    using KlusterKite.API.Tests.Mock;
    using KlusterKite.Security.Attributes;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Testing <see cref="ApiProvider"/> for resolving in various scenarios
    /// </summary>
    public class ApiProviderResolveTests
    {
        /// <summary>
        /// The output.
        /// </summary>
        private readonly ITestOutputHelper output;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiProviderResolveTests"/> class.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        public ApiProviderResolveTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Testing connection query resolve
        /// </summary>
        /// <param name="id">
        /// The id filter value
        /// </param>
        /// <param name="filterJson">
        /// The filter Json.
        /// </param>
        /// <param name="sortJson">
        /// The sort Json.
        /// </param>
        /// <param name="limit">
        /// The limit.
        /// </param>
        /// <param name="offset">
        /// The offset.
        /// </param>
        /// <param name="expectedCount">
        /// The expected Count.
        /// </param>
        /// <param name="expectedNames">
        /// The expected list of received object names.
        /// </param>
        /// <returns>
        /// The async task
        /// </returns>
        [Theory]
        [InlineData(null, null, null, 10, 0, 5, new[] { "1-test", "2-test", "3-test", "4-test", "5-test" })]
        [InlineData(null, null, "[\"value_asc\", \"name_desc\"]", 10, 0, 5, new[] { "5-test", "3-test", "2-test", "4-test", "1-test" })]
        [InlineData(null, null, "[\"value_desc\"]", 10, 0, 5, new[] { "1-test", "4-test", "2-test", "3-test", "5-test" })]

        [InlineData(null, null, "[\"type_asc\", \"name_asc\"]", 10, 0, 5, new[] { "1-test", "3-test", "5-test", "2-test", "4-test" })]

        [InlineData(null, "{\"value_lt\": 50}", null, 10, 0, 1, new[] { "5-test" })]
        [InlineData(null, "{\"value_lte\": 50}", null, 10, 0, 3, new[] { "2-test", "3-test", "5-test" })]
        [InlineData(null, "{\"value_not\": 50}", null, 10, 0, 3, new[] { "1-test", "4-test", "5-test" })]
        [InlineData(null, "{\"value\": 50}", null, 10, 0, 2, new[] { "2-test", "3-test" })]
        [InlineData(null, "{\"OR\": [{\"value\": 50}, {\"value\": 70}]}", null, 10, 0, 3, new[] { "2-test", "3-test", "4-test" })]
        [InlineData(null, "{\"AND\": [{\"value\": 50}, {\"name\": \"2-test\"}]}", null, 10, 0, 1, new[] { "2-test" })]

        [InlineData(null, "{\"name_in\": \"1-test, 3-test\"}", null, 10, 0, 2, new[] { "1-test", "3-test" })]
        [InlineData(null, "{\"name_not_in\": \"1-test, 3-test\"}", null, 10, 0, 3, new[] { "2-test", "4-test", "5-test" })]
        [InlineData(null, "{\"name_contains\": \"tes\"}", null, 10, 0, 5, new[] { "1-test", "2-test", "3-test", "4-test", "5-test" })]
        [InlineData(null, "{\"name_contains\": \"1-tes\"}", null, 10, 0, 1, new[] { "1-test" })]
        [InlineData(null, "{\"name_not_contains\": \"tes\"}", null, 10, 0, 0, new string[0])]
        [InlineData(null, "{\"name_not_contains\": \"1-tes\"}", null, 10, 0, 4, new[] { "2-test", "3-test", "4-test", "5-test" })]

        [InlineData(null, "{\"name_l_starts_with\": \"1-tes\"}", null, 10, 0, 1, new[] { "1-test" })]
        [InlineData(null, "{\"name_starts_with\": \"tes\"}", null, 10, 0, 0, new string[0])]
        [InlineData(null, "{\"name_not_starts_with\": \"1-tes\"}", null, 10, 0, 4, new[] { "2-test", "3-test", "4-test", "5-test" })]

        [InlineData(null, "{\"name_ends_with\": \"test\"}", null, 10, 0, 5, new[] { "1-test", "2-test", "3-test", "4-test", "5-test" })]
        [InlineData(null, "{\"name_ends_with\": \"tes\"}", null, 10, 0, 0, new string[0])]
        [InlineData(null, "{\"name_not_ends_with\": \"test\"}", null, 10, 0, 0, new string[0])]

        [InlineData(null, "{\"type\": \"Good\"}", null, 10, 0, 3, new[] { "1-test", "3-test", "5-test" })]

        [InlineData("24197905-2a1d-48bb-8781-5fc250cf8a35", null, null, 10, 0, 1, new[] { "3-test" })]
        [InlineData("24197905-2a1d-48bb-8781-5fc250cf8a35", "{\"value\": 50}", null, 10, 0, 1, new[] { "3-test" })]
        [InlineData("24197905-2a1d-48bb-8781-5fc250cf8a36", null, null, 10, 0, 0, new string[0])]
        [InlineData("24197905-2a1d-48bb-8781-5fc250cf8a35", "{\"value\": 100}", null, 10, 0, 0, new string[0])]

        [InlineData(null, null, null, 3, null, 5, new[] { "1-test", "2-test", "3-test" })]
        [InlineData(null, null, null, 3, 1, 5, new[] { "2-test", "3-test", "4-test" })]
        [InlineData(null, null, null, 3, 2, 5, new[] { "3-test", "4-test", "5-test" })]
        [InlineData(null, null, null, null, 2, 5, new[] { "3-test", "4-test", "5-test" })]
        [InlineData(null, null, null, 3, 3, 5, new[] { "4-test", "5-test" })]
        [InlineData(null, null, null, 3, 10, 5, new string[0])]
        public async Task ConnectionQueryTests(string id, string filterJson, string sortJson, int? limit, int? offset, int expectedCount, string[] expectedNames)
        {
            var uid1 = Guid.Parse("{4E4F28CD-EC25-48A1-9F87-F48700C7FABB}");
            var uid2 = Guid.Parse("{A67DF061-9A15-4CFD-BC47-050922E37AF5}");
            var uid3 = Guid.Parse("{24197905-2A1D-48BB-8781-5FC250CF8A35}");
            var uid4 = Guid.Parse("{8A06971D-7706-4D19-B0E8-A172D352D53E}");
            var uid5 = Guid.Parse("{1F2C2013-E636-4B7D-A018-1BD9AAA0D5A0}");

            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject { Name = "1-test", Value = 100m, Type = TestObject.EnObjectType.Good, Id = uid1 },
                                         new TestObject { Name = "2-test", Value = 50m, Type = TestObject.EnObjectType.Bad, Id = uid2 },
                                         new TestObject { Name = "3-test", Value = 50m, Type = TestObject.EnObjectType.Good, Id = uid3 },
                                         new TestObject { Name = "4-test", Value = 70m, Type = TestObject.EnObjectType.Bad, Id = uid4 },
                                         new TestObject { Name = "5-test", Value = 6m, Type = TestObject.EnObjectType.Good, Id = uid5 },
                                     };

            var provider = this.GetProvider(initialObjects);
            var context = new RequestContext();

            var objFields = new List<ApiRequest>
                                {
                                    new ApiRequest { FieldName = "id" },
                                    new ApiRequest { FieldName = "name" },
                                    new ApiRequest { FieldName = "value" }
                                };

            var connectionFields = new List<ApiRequest>
            {
                new ApiRequest { FieldName = "count" },
                new ApiRequest { FieldName = "items", Fields = objFields }
            };

            var arguments = new JObject();
            if (!string.IsNullOrWhiteSpace(id))
            {
                arguments.Add("id", id);
            }

            if (!string.IsNullOrWhiteSpace(filterJson))
            {
                arguments.Add("filter", JsonConvert.DeserializeObject(filterJson) as JToken);
            }

            if (!string.IsNullOrWhiteSpace(sortJson))
            {
                arguments.Add("sort", JsonConvert.DeserializeObject(sortJson) as JToken);
            }

            arguments.Add("limit", limit);
            arguments.Add("offset", offset);
            
            var query = new List<ApiRequest> { new ApiRequest { FieldName = "connection", Fields = connectionFields, Arguments = arguments } };
            var result = await this.Query(provider, query, context);

            Assert.NotNull(result);
            Assert.NotNull(result.Property("connection"));
            var connectionData = result.Property("connection").Value as JObject;
            Assert.NotNull(connectionData);
            Assert.NotNull(connectionData.Property("count"));
            Assert.Equal(expectedCount, connectionData.Property("count").Value.ToObject<int>());

            var nodes = connectionData.Property("items")?.Value as JArray;
            Assert.NotNull(nodes);
            Assert.Equal(expectedNames.Length, nodes.Count);
            Assert.Equal(
                string.Join(", ", expectedNames),
                string.Join(", ", nodes.Select(n => (n as JObject)?.Property("name").Value)));
        }

        /// <summary>
        /// Testing connection query resolve
        /// </summary>
        /// <param name="id">
        /// The id filter value
        /// </param>
        /// <param name="filterJson">
        /// The filter Json.
        /// </param>
        /// <param name="sortJson">
        /// The sort Json.
        /// </param>
        /// <param name="limit">
        /// The limit.
        /// </param>
        /// <param name="offset">
        /// The offset.
        /// </param>
        /// <param name="expectedCount">
        /// The expected Count.
        /// </param>
        /// <param name="expectedNames">
        /// The expected list of received object names.
        /// </param>
        /// <returns>
        /// The async task
        /// </returns>
        [Theory]
        [InlineData(null, null, null, 10, 0, 5, new[] { "1-test", "2-test", "3-test", "4-test", "5-test" })]
        [InlineData(null, null, "[\"value_asc\", \"name_desc\"]", 10, 0, 5, new[] { "5-test", "3-test", "2-test", "4-test", "1-test" })]
        [InlineData(null, null, "[\"value_desc\"]", 10, 0, 5, new[] { "1-test", "4-test", "2-test", "3-test", "5-test" })]

        [InlineData(null, null, "[\"type_asc\", \"name_asc\"]", 10, 0, 5, new[] { "1-test", "3-test", "5-test", "2-test", "4-test" })]

        [InlineData(null, "{\"value_lt\": 50}", null, 10, 0, 1, new[] { "5-test" })]
        [InlineData(null, "{\"value_lte\": 50}", null, 10, 0, 3, new[] { "2-test", "3-test", "5-test" })]
        [InlineData(null, "{\"value_not\": 50}", null, 10, 0, 3, new[] { "1-test", "4-test", "5-test" })]
        [InlineData(null, "{\"value\": 50}", null, 10, 0, 2, new[] { "2-test", "3-test" })]
        [InlineData(null, "{\"OR\": [{\"value\": 50}, {\"value\": 70}]}", null, 10, 0, 3, new[] { "2-test", "3-test", "4-test" })]
        [InlineData(null, "{\"AND\": [{\"value\": 50}, {\"name\": \"2-test\"}]}", null, 10, 0, 1, new[] { "2-test" })]

        [InlineData(null, "{\"name_in\": \"1-test, 3-test\"}", null, 10, 0, 2, new[] { "1-test", "3-test" })]
        [InlineData(null, "{\"name_not_in\": \"1-test, 3-test\"}", null, 10, 0, 3, new[] { "2-test", "4-test", "5-test" })]
        [InlineData(null, "{\"name_contains\": \"tes\"}", null, 10, 0, 5, new[] { "1-test", "2-test", "3-test", "4-test", "5-test" })]
        [InlineData(null, "{\"name_contains\": \"1-tes\"}", null, 10, 0, 1, new[] { "1-test" })]
        [InlineData(null, "{\"name_not_contains\": \"tes\"}", null, 10, 0, 0, new string[0])]
        [InlineData(null, "{\"name_not_contains\": \"1-tes\"}", null, 10, 0, 4, new[] { "2-test", "3-test", "4-test", "5-test" })]

        [InlineData(null, "{\"name_l_starts_with\": \"1-tes\"}", null, 10, 0, 1, new[] { "1-test" })]
        [InlineData(null, "{\"name_starts_with\": \"tes\"}", null, 10, 0, 0, new string[0])]
        [InlineData(null, "{\"name_not_starts_with\": \"1-tes\"}", null, 10, 0, 4, new[] { "2-test", "3-test", "4-test", "5-test" })]

        [InlineData(null, "{\"name_ends_with\": \"test\"}", null, 10, 0, 5, new[] { "1-test", "2-test", "3-test", "4-test", "5-test" })]
        [InlineData(null, "{\"name_ends_with\": \"tes\"}", null, 10, 0, 0, new string[0])]
        [InlineData(null, "{\"name_not_ends_with\": \"test\"}", null, 10, 0, 0, new string[0])]

        [InlineData(null, "{\"type\": \"Good\"}", null, 10, 0, 3, new[] { "1-test", "3-test", "5-test" })]

        [InlineData("24197905-2a1d-48bb-8781-5fc250cf8a35", null, null, 10, 0, 1, new[] { "3-test" })]
        [InlineData("24197905-2a1d-48bb-8781-5fc250cf8a35", "{\"value\": 50}", null, 10, 0, 1, new[] { "3-test" })]
        [InlineData("24197905-2a1d-48bb-8781-5fc250cf8a36", null, null, 10, 0, 0, new string[0])]
        [InlineData("24197905-2a1d-48bb-8781-5fc250cf8a35", "{\"value\": 100}", null, 10, 0, 0, new string[0])]

        [InlineData(null, null, null, 3, null, 5, new[] { "1-test", "2-test", "3-test" })]
        [InlineData(null, null, null, 3, 1, 5, new[] { "2-test", "3-test", "4-test" })]
        [InlineData(null, null, null, 3, 2, 5, new[] { "3-test", "4-test", "5-test" })]
        [InlineData(null, null, null, null, 2, 5, new[] { "3-test", "4-test", "5-test" })]
        [InlineData(null, null, null, 3, 3, 5, new[] { "4-test", "5-test" })]
        [InlineData(null, null, null, 3, 10, 5, new string[0])]
        public async Task CollectionQueryTests(string id, string filterJson, string sortJson, int? limit, int? offset, int expectedCount, string[] expectedNames)
        {
            var uid1 = Guid.Parse("{4E4F28CD-EC25-48A1-9F87-F48700C7FABB}");
            var uid2 = Guid.Parse("{A67DF061-9A15-4CFD-BC47-050922E37AF5}");
            var uid3 = Guid.Parse("{24197905-2A1D-48BB-8781-5FC250CF8A35}");
            var uid4 = Guid.Parse("{8A06971D-7706-4D19-B0E8-A172D352D53E}");
            var uid5 = Guid.Parse("{1F2C2013-E636-4B7D-A018-1BD9AAA0D5A0}");

            var initialObjects = new List<TestObject>
                                     {
                                         new TestObject { Name = "1-test", Value = 100m, Type = TestObject.EnObjectType.Good, Id = uid1 },
                                         new TestObject { Name = "2-test", Value = 50m, Type = TestObject.EnObjectType.Bad, Id = uid2 },
                                         new TestObject { Name = "3-test", Value = 50m, Type = TestObject.EnObjectType.Good, Id = uid3 },
                                         new TestObject { Name = "4-test", Value = 70m, Type = TestObject.EnObjectType.Bad, Id = uid4 },
                                         new TestObject { Name = "5-test", Value = 6m, Type = TestObject.EnObjectType.Good, Id = uid5 },
                                     };

            var provider = this.GetProvider(initialObjects);
            var context = new RequestContext();

            var objFields = new List<ApiRequest>
                                {
                                    new ApiRequest { FieldName = "id" },
                                    new ApiRequest { FieldName = "name" },
                                    new ApiRequest { FieldName = "value" }
                                };

            var connectionFields = new List<ApiRequest>
            {
                new ApiRequest { FieldName = "count" },
                new ApiRequest { FieldName = "items", Fields = objFields }
            };

            var arguments = new JObject();
            if (!string.IsNullOrWhiteSpace(id))
            {
                arguments.Add("id", id);
            }

            if (!string.IsNullOrWhiteSpace(filterJson))
            {
                arguments.Add("filter", JsonConvert.DeserializeObject(filterJson) as JToken);
            }

            if (!string.IsNullOrWhiteSpace(sortJson))
            {
                arguments.Add("sort", JsonConvert.DeserializeObject(sortJson) as JToken);
            }

            arguments.Add("limit", limit);
            arguments.Add("offset", offset);

            var query = new List<ApiRequest> { new ApiRequest { FieldName = "collection", Fields = connectionFields, Arguments = arguments } };
            var result = await this.Query(provider, query, context);

            Assert.NotNull(result);
            Assert.NotNull(result.Property("collection"));
            var connectionData = result.Property("collection").Value as JObject;
            Assert.NotNull(connectionData);
            Assert.NotNull(connectionData.Property("count"));
            Assert.Equal(expectedCount, connectionData.Property("count").Value.ToObject<int>());

            var nodes = connectionData.Property("items")?.Value as JArray;
            Assert.NotNull(nodes);
            Assert.Equal(expectedNames.Length, nodes.Count);
            Assert.Equal(
                string.Join(", ", expectedNames),
                string.Join(", ", nodes.Select(n => (n as JObject)?.Property("name").Value)));
        }

        /// <summary>
        /// Testing request for list with multiple type elements
        /// </summary>
        /// <returns>The async task</returns>
        [Fact(Skip = "TBD")]
        public async Task TypedCollectionTest()
        {
            var provider = this.GetProvider();
            var context = new RequestContext();

            var objFields = new List<ApiRequest>
                                       {
                                           new ApiRequest { FieldName = "id" },
                                           new ApiRequest { FieldName = "__type" },
                                           new ApiRequest { FieldName = "@TestLogFirst:firstMessage" },
                                           new ApiRequest { FieldName = "@TestLogSecond:secondMessage" },
                                       };

            var connectionFields = new List<ApiRequest>
                                       {
                                           new ApiRequest { FieldName = "count" },
                                           new ApiRequest { FieldName = "items", Fields = objFields }
                                       };

            var query = new List<ApiRequest> { new ApiRequest { FieldName = "multipleEndClassArray", Fields = connectionFields } };
            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("multipleEndClassArray"));
             
            Assert.Equal(1, result.SelectToken("multipleEndClassArray.items[0].id")?.Value<int>());
            Assert.Equal("TestLogFirst", result.SelectToken("multipleEndClassArray.items[0].__type")?.Value<string>());
            Assert.Null(result.SelectToken("multipleEndClassArray.items[0].secondMessage"));
            Assert.Equal("first", result.SelectToken("multipleEndClassArray.items[0].firstMessage")?.Value<string>());

            Assert.Equal(1, result.SelectToken("multipleEndClassArray.items[1].id")?.Value<int>());
            Assert.Equal("TestLogSecond", result.SelectToken("multipleEndClassArray.items[1].__type")?.Value<string>());
            Assert.Null(result.SelectToken("multipleEndClassArray.items[1].firstMessage"));
            Assert.Equal("second", result.SelectToken("multipleEndClassArray.items[1].secondMessage")?.Value<string>());
        }

        /// <summary>
        /// Testing connection mutation resolve
        /// </summary>
        /// <param name="mutationName">
        /// The mutation name to call
        /// </param>
        /// <param name="mutationRequest">
        /// The mutation arguments
        /// </param>
        /// <param name="expectResult">
        /// A value indicating whether to expect result or error
        /// </param>
        /// <param name="expectedResult">
        /// The expected value (or errors if no result expected).
        /// </param>
        /// <param name="expectConnectionFormat">
        /// A value indicating whether to expect response in connection mutation result format
        /// </param>
        /// <returns>
        /// The async task
        /// </returns>
        [Theory]
        [InlineData("connection.create", "{\"newNode\": { \"name\":\"6-test\", \"value\": 1 }}", true, "{\"name\":\"6-test\",\"value\": 1.0 }", true)]
        [InlineData("connection.create", "{\"newNode\": { \"value\": 1 }}", false, "Create failed; name should be set", true)]
        [InlineData("connection.create", null, false, "Create failed; object data was not provided", true)]
        [InlineData("connection.update", "{\"id\": \"B500CA20-F649-4DCD-BDA8-1FA5031ECDD3\", \"newNode\": { \"value\": 1.0 }}", true, "{\"name\":\"2-test\",\"value\": 1.0 }", true)]
        [InlineData("connection.update", "{\"id\": \"B500CA20-F649-4DCD-BDA8-1FA5031ECDD3\", \"newNode\": { \"id\": \"{C12EE96B-2420-4F54-AAE5-788995B10679}\" }}", true, "{\"name\":\"2-test\",\"value\": 50.0 }", true)]
        [InlineData("connection.update", "{\"id\": \"B500CA20-F649-4DCD-BDA8-1FA5031ECDD4\", \"newNode\": { \"value\": 1.0 }}", false, "Update failed; Node not found", true)]
        [InlineData("connection.update", "{\"id\": \"B500CA20-F649-4DCD-BDA8-1FA5031ECDD3\", \"newNode\": { \"id\": \"{F0607502-5B77-4A3C-9142-E6197A7EE61E}\" }}", false, "Update failed; Duplicate key", true)]
        [InlineData("connection.delete", "{\"id\": \"B500CA20-F649-4DCD-BDA8-1FA5031ECDD3\"}", true, "{\"name\":\"2-test\",\"value\": 50.0 }", true)]
        [InlineData("connection.delete", "{\"id\": \"B500CA20-F649-4DCD-BDA8-1FA5031ECDD4\"}", false, "Delete failed; Node not found", true)]
        [InlineData("connection.typedMutation", "{\"uid\": \"B500CA20-F649-4DCD-BDA8-1FA5031ECDD3\"}", true, "{\"name\":\"2-test\",\"value\": 50.0 }", true)]
        [InlineData("connection.untypedMutation", "{\"uid\": \"B500CA20-F649-4DCD-BDA8-1FA5031ECDD3\"}", true, "{\"result\": true}", false)]
        public async Task ConnectionMutationTests(
            string mutationName,
            string mutationRequest,
            bool expectResult,
            string expectedResult,
            bool expectConnectionFormat)
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

            var provider = this.GetProvider(initialObjects);
            var context = new RequestContext();
            var errorItemsRequest = new ApiRequest
            {
                FieldName = "items",
                Fields =
                                           new List<ApiRequest>
                                               {
                                                   new ApiRequest { FieldName = "number" },
                                                   new ApiRequest { FieldName = "field" },
                                                   new ApiRequest { FieldName = "message" }
                                               }
            };

            var errorRequest = new ApiRequest
                                   {
                                       FieldName = "errors",
                                       Fields =
                                           new List<ApiRequest>
                                               {
                                                   new ApiRequest { FieldName = "count" },
                                                   errorItemsRequest
                                               }
                                   };
            var resultRequest = new ApiRequest
                                    {
                                        FieldName = "result",
                                        Fields =
                                            new List<ApiRequest>
                                                {
                                                    new ApiRequest { FieldName = "name" },
                                                    new ApiRequest { FieldName = "value" }
                                                }
                                    };
            var request = new ApiRequest
                              {
                                  FieldName = mutationName,
                                  Arguments =
                                      string.IsNullOrWhiteSpace(mutationRequest)
                                          ? null
                                          : JsonConvert.DeserializeObject(mutationRequest) as JObject,
                                  Fields = new List<ApiRequest> { resultRequest, errorRequest, }
                              };

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var result = await provider.ResolveMutation(
                             request,
                             context,
                             e => this.output.WriteLine($"Resolve error: {e.Message}\n{e.StackTrace}"));
            stopwatch.Stop();
            this.output.WriteLine($"Resolved in {(double)stopwatch.ElapsedTicks * 1000 / Stopwatch.Frequency}ms");
            Assert.NotNull(result);
            this.output.WriteLine(result.ToString(Formatting.Indented));

            if (expectConnectionFormat)
            {
                var resultObject = result.Property("result");
                var resultErrors = result.Property("errors");
                Assert.NotNull(resultObject);
                Assert.True(resultObject.HasValues);

                Assert.NotNull(resultErrors);
                Assert.True(resultErrors.HasValues);

                if (expectResult)
                {
                    Assert.False(resultErrors.Value.HasValues);
                    Assert.True(resultObject.Value.HasValues);
                    Assert.Equal(
                        ((JObject)JsonConvert.DeserializeObject(expectedResult)).ToString(Formatting.None),
                        resultObject.Value.ToString(Formatting.None));
                }
                else
                {
                    Assert.True(resultErrors.Value.HasValues);
                    Assert.False(resultObject.Value.HasValues);
                    var errors = string.Join(
                        "; ",
                        resultErrors.Value.SelectTokens("items[*].message").Select(t => t.Value<string>()).ToList());
                    Assert.Equal(expectedResult, errors);
                }
            }
            else
            {
                Assert.Equal(
                        ((JObject)JsonConvert.DeserializeObject(expectedResult)).ToString(Formatting.None),
                        result.ToString(Formatting.None));
            }
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task AsyncArrayOfScalarFieldTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var query = new List<ApiRequest> { new ApiRequest { FieldName = "asyncArrayOfScalarField" } };

            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("asyncArrayOfScalarField"));
            var array = (decimal[])result.Property("asyncArrayOfScalarField").Value.ToObject(typeof(decimal[]));
            Assert.NotNull(array);
            Assert.Equal(2, array.Length);
            Assert.Equal(4M, array[0]);
            Assert.Equal(5M, array[1]);
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task AsyncFrowardedScalarFieldTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var query = new List<ApiRequest> { new ApiRequest { FieldName = "asyncForwardedScalar" } };
            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("asyncForwardedScalar"));
            Assert.Equal("AsyncForwardedScalar", result.Property("asyncForwardedScalar").ToObject<string>());
        }

        /// <summary>
        /// Testing sync nested scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task AsyncNestedScalarFieldTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var query = new List<ApiRequest>
                            {
                                new ApiRequest
                                    {
                                        FieldName = "nestedAsync",
                                        Fields =
                                            new List<ApiRequest>
                                                {
                                                    new ApiRequest
                                                        {
                                                            FieldName = "asyncScalarField"
                                                        }
                                                }
                                    }
                            };
            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("nestedAsync"));
            Assert.IsType<JObject>(result.Property("nestedAsync").Value);
            var nested = (JObject)result.Property("nestedAsync").Value;
            Assert.Equal("AsyncScalarField", nested.Property("asyncScalarField")?.ToObject<string>());
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task AsyncObjectMethodTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var methodParameters =
                "{\"intArrayArg\": [7, 8, 9], \"intArg\": 1, \"stringArg\": \"test\", \"objArg\": {syncScalarField: \"nested test\"}}";
            var query = new List<ApiRequest>
                            {
                                new ApiRequest
                                    {
                                        FieldName = "asyncObjectMethod",
                                        Arguments =
                                            (JObject)
                                            JsonConvert.DeserializeObject(methodParameters),
                                        Fields =
                                            new List<ApiRequest>
                                                {
                                                    new ApiRequest
                                                        {
                                                            FieldName
                                                                =
                                                                "syncScalarField"
                                                        }
                                                }
                                    }
                            };

            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("asyncObjectMethod")?.Value as JObject);
            var resultObject = (JObject)result.Property("asyncObjectMethod").Value;
            Assert.Equal("returned type", resultObject.Property("syncScalarField").ToObject<string>());
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task AsyncScalarFieldTest()
        {
            var provider = this.GetProvider();
            Assert.Equal(0, provider.GenerationErrors.Count);

            var context = new RequestContext();
            var query = new List<ApiRequest> { new ApiRequest { FieldName = "asyncScalarField" } };
            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("asyncScalarField"));
            Assert.Equal("AsyncScalarField", result.Property("asyncScalarField").ToObject<string>());
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task FaultedASyncMethodTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var query = new List<ApiRequest>
                            {
                                new ApiRequest
                                    {
                                        FieldName = "faultedASyncMethod",
                                        Fields =
                                            new List<ApiRequest>
                                                {
                                                    new ApiRequest
                                                        {
                                                            FieldName
                                                                =
                                                                "syncScalarField"
                                                        }
                                                }
                                    }
                            };

            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("faultedASyncMethod"));
            Assert.False(result.Property("faultedASyncMethod").Value.HasValues);
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task SyncArrayOfScalarFieldTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var query = new List<ApiRequest> { new ApiRequest { FieldName = "syncArrayOfScalarField" } };

            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("syncArrayOfScalarField"));
            var array = (int[])result.Property("syncArrayOfScalarField").Value.ToObject(typeof(int[]));
            Assert.NotNull(array);
            Assert.Equal(3, array.Length);
            Assert.Equal(1, array[0]);
            Assert.Equal(2, array[1]);
            Assert.Equal(3, array[2]);
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task SyncFaultedScalarFieldTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var query = new List<ApiRequest> { new ApiRequest { FieldName = "faultedSyncField" } };

            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("faultedSyncField"));
            Assert.False(result.Property("faultedSyncField").Value.HasValues);
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task SyncFrowardedArrayOfScalarFieldTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var query = new List<ApiRequest> { new ApiRequest { FieldName = "forwardedArray" } };

            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("forwardedArray"));
            var array = (int[])result.Property("forwardedArray").Value.ToObject(typeof(int[]));
            Assert.NotNull(array);
            Assert.Equal(3, array.Length);
            Assert.Equal(5, array[0]);
            Assert.Equal(6, array[1]);
            Assert.Equal(7, array[2]);
        }

        /// <summary>
        /// Testing sync nested scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task SyncNestedScalarFieldTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var query = new List<ApiRequest>
                            {
                                new ApiRequest
                                    {
                                        FieldName = "nestedSync",
                                        Fields =
                                            new List<ApiRequest>
                                                {
                                                    new ApiRequest
                                                        {
                                                            FieldName
                                                                =
                                                                "syncScalarField"
                                                        }
                                                }
                                    }
                            };
            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("nestedSync"));
            Assert.IsType<JObject>(result.Property("nestedSync").Value);
            var nested = (JObject)result.Property("nestedSync").Value;
            Assert.Equal("SyncScalarField", nested.Property("syncScalarField")?.ToObject<string>());
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task RecursionFieldTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var query = new List<ApiRequest> { new ApiRequest { FieldName = "syncScalarField" } };
            query = new List<ApiRequest> { new ApiRequest { FieldName = "recursion", Fields = query } };
            query = new List<ApiRequest> { new ApiRequest { FieldName = "recursion", Fields = query } };

            var result = await this.Query(provider, query, context);
            
            Assert.NotNull(result);

            result = result.Property("recursion")?.Value as JObject;
            Assert.NotNull(result);

            result = result.Property("recursion")?.Value as JObject;
            Assert.NotNull(result);

            Assert.Equal("SyncScalarField", result.Property("syncScalarField").ToObject<string>());
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task SyncScalarFieldTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var query = new List<ApiRequest> { new ApiRequest { FieldName = "syncScalarField" } };

            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("syncScalarField"));
            Assert.Equal("SyncScalarField", result.Property("syncScalarField").ToObject<string>());
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task ConverterTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var query = new List<ApiRequest> { new ApiRequest { FieldName = "thirdParty" } };

            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("thirdParty"));
            Assert.Equal("Third party", result.Property("thirdParty").ToObject<string>());
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task ConverterListTest()
        {
            var provider = this.GetProvider();
            provider.ThirdParties = new List<ThirdPartyObject>
                                        {
                                            new ThirdPartyObject("test1"),
                                            new ThirdPartyObject("test2")
                                        };

            var context = new RequestContext();
            var query = new List<ApiRequest> { new ApiRequest { FieldName = "thirdParties" } };

            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("thirdParties"));
            Assert.Equal(
                "test1, test2", 
                string.Join(", ", result.Property("thirdParties").Value.ToObject<string[]>()));
        }

        /// <summary>
        /// Testing sync scalar enum field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task SyncScalarEnumFieldTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var query = new List<ApiRequest>
                            {
                                new ApiRequest { FieldName = "syncEnumField" },
                                new ApiRequest { FieldName = "syncFlagsField" },
                                new ApiRequest { FieldName = "syncEnumNullableField" },
                                new ApiRequest { FieldName = "syncEnumNullableNullField" }
                            };

            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("syncEnumField"));
            Assert.Equal("EnumItem1", result.Property("syncEnumField").ToObject<string>());

            Assert.NotNull(result.Property("syncFlagsField"));
            Assert.Equal(1, result.Property("syncFlagsField").ToObject<int>());

            Assert.NotNull(result.Property("syncEnumNullableField"));
            Assert.Equal("EnumItem1", result.Property("syncEnumNullableField").ToObject<string>());

            Assert.NotNull(result.Property("syncEnumNullableNullField"));
            Assert.Equal(JValue.CreateNull(), result.Property("syncEnumNullableNullField").Value);
        }

        /// <summary>
        /// Testing sync scalar field
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task SyncScalarMethodTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();
            var methodParameters =
                "{\"intArg\": 1, \"stringArg\": \"test\", \"objArg\": {syncScalarField: \"nested test\"}}";
            var query = new List<ApiRequest>
                            {
                                new ApiRequest
                                    {
                                        FieldName = "syncScalarMethod",
                                        Arguments =
                                            (JObject)
                                            JsonConvert.DeserializeObject(methodParameters)
                                    }
                            };

            var result = await this.Query(provider, query, context);
            Assert.NotNull(result);
            Assert.NotNull(result.Property("syncScalarMethod"));
            Assert.Equal("ok", result.Property("syncScalarMethod").ToObject<string>());
        }

        /// <summary>
        /// Testing non-connection mutation
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task MutationTest()
        {
            var provider = this.GetProvider();

            var context = new RequestContext();

            const string MethodParameters = "{ \"name\": \"new name\"}";

            var request = new ApiRequest
                              {
                                  FieldName = "nestedSync.setName",
                                  Arguments = (JObject)JsonConvert.DeserializeObject(MethodParameters),
                                  Fields =
                                      new List<ApiRequest>
                                          {
                                              new ApiRequest { FieldName = "id" },
                                              new ApiRequest { FieldName = "name" },
                                              new ApiRequest { FieldName = "value" }
                                          }
                              };

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var result = await provider.ResolveMutation(
                             request,
                             context,
                             e => this.output.WriteLine($"Resolve error: {e.Message}\n{e.StackTrace}"));
            stopwatch.Stop();
            this.output.WriteLine($"Resolved in {(double)stopwatch.ElapsedTicks * 1000 / Stopwatch.Frequency}ms");
            Assert.NotNull(result);
            this.output.WriteLine(result.ToString(Formatting.Indented));
        }

        /// <summary>
        /// Gets the api provider
        /// </summary>
        /// <param name="objects">
        /// The initial objects list.
        /// </param>
        /// <returns>
        /// The api provider
        /// </returns>
        private TestProvider GetProvider(List<TestObject> objects = null)
        {
            var provider = new TestProvider(objects);
            foreach (var error in provider.GenerationErrors)
            {
                this.output.WriteLine($"Error: {error}");
            }

            Assert.Equal(0, provider.GenerationErrors.Count);
            return provider;
        }

        /// <summary>
        /// Executes the query
        /// </summary>
        /// <param name="provider">The provider</param>
        /// <param name="query">The query</param>
        /// <param name="context">The request context</param>
        /// <returns >Result of the execution</returns>
        private async Task<JObject> Query(TestProvider provider, List<ApiRequest> query, RequestContext context)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var result = await provider.ResolveQuery(
                             query,
                             context,
                             e => this.output.WriteLine($"Resolve error: {e.Message}\n{e.StackTrace}"));
            stopwatch.Stop();
            this.output.WriteLine($"Resolved in {(double)stopwatch.ElapsedTicks * 1000 / Stopwatch.Frequency}ms");
            this.output.WriteLine(result.ToString(Formatting.Indented));
            return result as JObject;
        }
    }
}