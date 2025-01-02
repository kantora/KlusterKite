// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ApiTests.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Testing <see cref="ApiProvider" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Monitoring.Tests
{
    using System.Collections.Generic;

    using Akka.Actor;
    using GraphQL;
    using GraphQL.Utilities;

    using KlusterKite.Web.GraphQL.Publisher;

    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Testing <see cref="MonitoringApiProvider"/>
    /// </summary>
    public class ApiTests
    {
        /// <summary>
        /// The test output stream
        /// </summary>
        private readonly ITestOutputHelper output;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiTests"/> class.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        public ApiTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// Testing the api generation
        /// </summary>
        [Fact]
        public void ApiGenerationTest()
        {
            var system = ActorSystem.Create("test");
            var api = new MonitoringApiProvider(system);

            foreach (var error in api.GenerationErrors)
            {
                this.output.WriteLine($"Error: {error}");
            }

            Assert.Empty(api.GenerationErrors);

            var webApiProvider = new DirectProvider(api, this.output.WriteLine) { UseJsonRepack = true };
            var schema = SchemaGenerator.Generate(new List<ApiProvider> { webApiProvider });
            var hasSchemaErrors = false;
            foreach (var error in SchemaGenerator.CheckSchema(schema))
            {
                hasSchemaErrors = true;
                this.output.WriteLine($"Schema error: {error}");
            }

            var description = schema.Print();
            this.output.WriteLine("-------- Schema -----------");
            this.output.WriteLine(description);
            Assert.False(string.IsNullOrWhiteSpace(description));


            Assert.False(hasSchemaErrors);

            hasSchemaErrors = false;
            foreach (var error in SchemaGenerator.CheckSchemaIntrospection(schema))
            {
                hasSchemaErrors = true;
                this.output.WriteLine($"Schema introspection error: {error}");
            }

            Assert.False(hasSchemaErrors);
        }
    }
}
