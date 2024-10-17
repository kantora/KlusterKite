// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ApiIntegrationTests.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Integration tests of NodeManager API
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    using Akka.Configuration;
    using Akka.Event;
    using Autofac;

    using KlusterKite.Core;
    using KlusterKite.Core.TestKit;
    using KlusterKite.Data.EF;
    using KlusterKite.Data.EF.InMemory;
    using KlusterKite.NodeManager.Authentication.Clients;
    using KlusterKite.NodeManager.ConfigurationSource;
    using KlusterKite.NodeManager.ConfigurationSource.Seeder;
    using KlusterKite.NodeManager.Launcher.Utils;
    using KlusterKite.NodeManager.Tests.Mock;
    using KlusterKite.Security.Attributes;
    using KlusterKite.Web;
    using KlusterKite.Web.GraphQL.Publisher;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using RestSharp;
    using RestSharp.Authenticators;
    using RestSharp.Authenticators.OAuth2;
    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Integration tests of NodeManager API
    /// </summary>
    public class ApiIntegrationTests : BaseActorTest<ApiIntegrationTests.Configuration>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiIntegrationTests"/> class.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        public ApiIntegrationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// Current bind port
        /// </summary>
        private int Port => this.Sys.Settings.Config.GetInt("KlusterKite.Web.WebHostPort");

        /// <summary>
        /// System start check
        /// </summary>
        /// <returns>
        /// The async task
        /// </returns>
        [Fact]
        public async Task StartUpTest()
        {
            this.Sys.StartNameSpaceActorsFromConfiguration();
            this.ExpectNoMsg(TimeSpan.FromSeconds(1));
            var options = new RestClientOptions($"http://localhost:{this.Port}")
            {
                Timeout = new TimeSpan(0, 0, 5)
            };
            var client = new RestClient(options);
            var token = await Authenticate(client);

            options.Authenticator = new OAuth2AuthorizationRequestHeaderAuthenticator(token, "Bearer");
            client = new RestClient(options);

            var query = @"
                {
                  api {
                    me {
                      klusterKiteUser {
                        login
                      }
                    }
                  }
                }
             ";

            var request = new RestRequest { Resource = "/api/1.x/graphQL/", Method = Method.Post };
            request.AddHeader("Accept", "application/json, text/json");
            var body = $"{{\"query\": {JsonConvert.SerializeObject(query)}, \"variables\": null}}";
            request.AddParameter("application/json", body, ParameterType.RequestBody);
            var result = await client.ExecuteAsync(request);

            Assert.Equal(ResponseStatus.Completed, result.ResponseStatus);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);

            var data = JObject.Parse(result.Content);
            var userName = data.SelectToken("data.api.me.klusterKiteUser.login");
            Assert.Equal("admin", userName?.Value<string>());
        }

        /// <summary>
        /// System start check
        /// </summary>
        /// <returns>
        /// The async task
        /// </returns>
        [Fact]
        public async Task RoleUpdate()
        {
            this.Sys.StartNameSpaceActorsFromConfiguration();
            this.ExpectNoMsg(TimeSpan.FromSeconds(1));
            var options = new RestClientOptions($"http://localhost:{this.Port}")
            {
                Timeout = new TimeSpan(0, 0, 5)
            };
            var client = new RestClient(options);

            var token = await Authenticate(client);

            var contextFactory = this.Container.Resolve<UniversalContextFactory>();
            Guid roleUid;

            using (var context = contextFactory.CreateContext<ConfigurationContext>(
                this.Sys.Settings.Config.GetString(NodeManagerActor.ConfigDatabaseProviderNamePath),
                this.Sys.Settings.Config.GetString(NodeManagerActor.ConfigConnectionStringPath),
                this.Sys.Settings.Config.GetString(NodeManagerActor.ConfigDatabaseNamePath)))
            {
                roleUid = context.Roles.First(r => r.Name == "Guest").Uid;
            }

            var query = @"
                mutation UpdateRoleMutation($input_0: KlusterKiteNodeApi_klusterKiteNodesApi_roles_update_Input!) {
                  klusterKiteNodeApi_klusterKiteNodesApi_roles_update(input: $input_0) {
                    clientMutationId,
                    node {
                      uid
                      name
                    }
                  }
                }
             ";

            var variables = $@"
            {{
              ""input_0"": 
              {{
                ""id"":""{roleUid:D}"",
                ""newNode"":
                {{
                  ""uid"":""{roleUid:D}"",
                  ""name"":""Guest"",
                  ""allowedScope"":[""KlusterKite.NodeManager.GetActiveNodeDescriptions"",""KlusterKite.NodeManager.GetTemplateStatistics"",""KlusterKite.NodeManager.Configuration.Query""],
                  ""deniedScope"":[]
                }},
                ""clientMutationId"":""0""
              }}
            }}
            ";

            var request = new RestRequest { Resource = "/api/1.x/graphQL/", Method = Method.Post };
            options.Authenticator = new OAuth2AuthorizationRequestHeaderAuthenticator(token, "Bearer");
            client = new RestClient(options);
            request.AddHeader("Accept", "application/json, text/json");
            var body = $"{{\"query\": {JsonConvert.SerializeObject(query)}, \"variables\": {variables}}}";
            request.AddParameter("application/json", body, ParameterType.RequestBody);
            var result = await client.ExecuteAsync(request);

            Assert.Equal(ResponseStatus.Completed, result.ResponseStatus);

            if (result.Content != null)
            {
                this.Sys.Log.Log(LogLevel.InfoLevel, null, "RESPONSE CONTENT: {content}", result.Content);
            }

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);

            var data = JObject.Parse(result.Content);
            var clientMutationId = data.SelectToken("data.klusterKiteNodeApi_klusterKiteNodesApi_roles_update.clientMutationId");
            Assert.Equal("0", clientMutationId?.Value<string>());
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            Startup.Reset();
            base.Dispose(disposing);
        }

        /// <summary>
        /// Authenticate as admin user
        /// </summary>
        /// <param name="client">The rest client</param>
        /// <returns>The access token</returns>
        private static async Task<string> Authenticate(RestClient client)
        {
            var authenticationRequest = new RestRequest { Method = Method.Post, Resource = "/api/1.x/security/token" };
            authenticationRequest.AddParameter("grant_type", "password");
            authenticationRequest.AddParameter("username", "admin");
            authenticationRequest.AddParameter("password", "admin");
            authenticationRequest.AddParameter("client_id", WebApplication.WebApplicationClientId);
            var authenticationResult = await client.ExecuteAsync<TokenResponse>(authenticationRequest);

            Assert.Equal(ResponseStatus.Completed, authenticationResult.ResponseStatus);
            Assert.Equal(HttpStatusCode.OK, authenticationResult.StatusCode);
            Assert.NotNull(authenticationResult.Data);
            Assert.NotNull(authenticationResult.Data.AccessToken);
            return authenticationResult.Data.AccessToken;
        }

        /// <summary>
        /// The test environment configuration
        /// </summary>
        public class Configuration : TestConfigurator
        {
            /// <inheritdoc />
            public override bool RunPostStart => true;

            /// <inheritdoc />
            public override Config GetAkkaConfig(ContainerBuilder containerBuilder)
            {
                var listener = new TcpListener(IPAddress.Any, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();

                var config = $@"
                 KlusterKite {{
                    NodeManager.ConfigurationDatabaseConnectionString = ""test""
	                NodeManager.ConfigurationDatabaseProviderName = ""InMemory""
                    NodeManager.RegisterNuget = false
                    Web {{
                            WebHostPort = {port},
 			                BindAddress = ""http://*:{port}"",
                            Debug.Trace = true
                    }}

                  }}

                  akka.actor.deployment {{
  

                        /NodeManager {{
                                IsNameSpace = true
                        }}

                        /NodeManager/NodeManagerProxy {{
                                actor-type = Simple
                                type = ""KlusterKite.NodeManager.NodeManagerActor, KlusterKite.NodeManager""       
                        }}

                        /NodeManager/NodeManagerProxy/workers {{
                                router = consistent-hashing-pool
                                nr-of-instances = 1                                                    
                        }} 
                }}

                ";

                return ConfigurationFactory.ParseString(config).WithFallback(base.GetAkkaConfig(containerBuilder));
            }

            /// <inheritdoc />
            public override List<BaseInstaller> GetPluginInstallers()
            {
                // ReSharper disable RedundantNameQualifier
                return new BaseInstaller[]
                           {
                               new KlusterKite.API.Endpoint.Installer(), new KlusterKite.Data.Installer(),
                               new KlusterKite.Data.EF.Installer(), new KlusterKite.Data.EF.InMemory.Installer(),
                               new KlusterKite.Core.Installer(), new KlusterKite.Core.TestKit.Installer(),
                               new KlusterKite.Web.Installer(), new KlusterKite.Web.GraphQL.Publisher.Installer(),
                               new KlusterKite.Web.Authorization.Installer(),
                               new KlusterKite.Web.Authentication.Installer(),
                               new KlusterKite.NodeManager.Client.Installer(),
                               new KlusterKite.NodeManager.ConfigurationSource.Installer(),
                               new KlusterKite.NodeManager.Authentication.Installer(),
                               new KlusterKite.NodeManager.Installer(),
                               new Installer(),
                           }.ToList();

                // ReSharper restore RedundantNameQualifier
            }
        }

        /// <summary>
        /// The test installer
        /// </summary>
        public class Installer : BaseInstaller
        {
            /// <inheritdoc />
            protected override decimal AkkaConfigLoadPriority => -1M;

            /// <inheritdoc />
            protected override Config GetAkkaConfig()
            {
                return Config.Empty;
            }

            /// <inheritdoc />
            protected override void RegisterComponents(ContainerBuilder container, Config config)
            {
                container.RegisterInstance(new MoqTokenManager()).As<ITokenManager>();
                container.RegisterInstance(TestRepository.CreateRepositoryFromLoadedAssemblies()).As<IPackageRepository>();
            }

            /// <inheritdoc />
            protected override void PostStart(IComponentContext componentContext)
            {
                base.PostStart(componentContext);
                var contextManager = componentContext.Resolve<UniversalContextFactory>();
                var config = componentContext.Resolve<Config>();
                var connectionString = config.GetString(NodeManagerActor.ConfigConnectionStringPath);
                var repository = componentContext.Resolve<IPackageRepository>();
                var databaseName = config.GetString(NodeManagerActor.ConfigDatabaseNamePath);
                using (var context =
                    contextManager.CreateContext<ConfigurationContext>("InMemory", connectionString, databaseName))
                {
                    context.ResetValueGenerators();
                    context.Database.EnsureDeleted();
                    var seeder = new Seeder(config, contextManager, repository);
                    Serilog.Log.Information("!!!!!!!!!!! Seeding started");
                    seeder.Seed();
                    Serilog.Log.Information("!!!!!!!!!!! Seeding finished");
                }

                var schemaProvider = componentContext.Resolve<SchemaProvider>();

                var apiProviders = componentContext.Resolve<IEnumerable<API.Provider.ApiProvider>>()
                    .Select(p => new DirectProvider(p, error => Serilog.Log.Error("API ERROR: {error}", error)))
                    .Cast<ApiProvider>()
                    .ToList();

                schemaProvider.CurrentSchema = SchemaGenerator.Generate(apiProviders);
            }
        }
    }
}