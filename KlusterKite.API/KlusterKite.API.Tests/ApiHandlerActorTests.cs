// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ApiHandlerActorTests.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Testing the <see cref="ApiHandlerActor" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.API.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Akka.Actor;
    using Akka.Event;

    using KlusterKite.API.Client;
    using KlusterKite.API.Client.Messages;
    using KlusterKite.API.Endpoint;
    using KlusterKite.API.Tests.Mock;
    using KlusterKite.Core;
    using KlusterKite.Core.TestKit;
    using KlusterKite.Security.Attributes;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Testing the <see cref="ApiHandlerActor"/>
    /// </summary>
    public class ApiHandlerActorTests : BaseActorTest<ApiPublisherActorTests.Configurator>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiHandlerActorTests"/> class.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        public ApiHandlerActorTests(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// Checking the discovery request
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task QueryHandleTest()
        {
            this.ExpectNoMsg();
            var actor =
                this.ActorOfAsTestActorRef<ApiHandlerActor>(Props.Create(() => new ApiHandlerActor(new TestProvider(null))));

            var context = new RequestContext();
            var queryFields = new List<ApiRequest> { new ApiRequest { FieldName = "asyncScalarField" } };
            var query = new QueryApiRequest { Context = context, Fields = queryFields };

            var result = (JObject)await actor.Ask<SurrogatableJObject>(query, TimeSpan.FromSeconds(1));
            Assert.NotNull(result);
            Assert.NotNull(result.Property("asyncScalarField"));
            Assert.Equal("AsyncScalarField", result.Property("asyncScalarField").ToObject<string>());
        }

        /// <summary>
        /// Checking the discovery request
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task MutationHandleTest()
        {
            this.ExpectNoMsg();
            var actor =
                this.ActorOfAsTestActorRef<ApiHandlerActor>(Props.Create(() => new ApiHandlerActor(new TestProvider(null))));

            var context = new RequestContext();

            const string MethodParameters = "{ \"name\": \"new name\"}";

            var request = new MutationApiRequest
            {
                FieldName = "nestedSync.setName",
                Arguments = (JObject)JsonConvert.DeserializeObject(MethodParameters),
                Fields = new List<ApiRequest>
                                          {
                                              new ApiRequest { FieldName = "id" },
                                              new ApiRequest { FieldName = "name" },
                                              new ApiRequest { FieldName = "value" }
                                          },
                Context = context
            };

            var result = (JObject)await actor.Ask<SurrogatableJObject>(request, TimeSpan.FromSeconds(1));
            Assert.NotNull(result);
            this.Sys.Log.Info(result.ToString(Formatting.Indented));
            var token = result.SelectToken("result.name");
            Assert.NotNull(token);
            Assert.Equal("new name", token.ToObject<string>());
        }

        /// <summary>
        /// Configures current test system
        /// </summary>
        public class Configurator : TestConfigurator
        {
            /// <summary>
            /// Gets list of all used plugin installers
            /// </summary>
            /// <returns>The list of installers</returns>
            public override List<BaseInstaller> GetPluginInstallers()
            {
                var pluginInstallers = new List<BaseInstaller>
                                           {
                                               new Core.Installer(),
                                               new Core.TestKit.Installer()
                                           };
                return pluginInstallers;
            }
        }
    }
} 