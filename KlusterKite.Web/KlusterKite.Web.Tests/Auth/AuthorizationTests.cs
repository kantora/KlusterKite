// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AuthorizationTests.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Testing authorization process
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace KlusterKite.Web.Tests.Auth
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    using Akka.Configuration;

    using Autofac;

    using KlusterKite.Core;
    using KlusterKite.Core.TestKit;
    using KlusterKite.Security.Attributes;
    using KlusterKite.Web.Tests.Controllers;

    using RestSharp;
    using RestSharp.Authenticators;

    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Testing authorization process
    /// </summary>
    public class AuthorizationTests : WebTest<AuthorizationTests.Configurator>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationTests"/> class.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        public AuthorizationTests(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// The type of test authentication
        /// </summary>
        public enum EnAuthenticationType
        {
            /// <summary>
            /// There is no authentication
            /// </summary>
            None,

            /// <summary>
            /// There is user authentication
            /// </summary>
            User,

            /// <summary>
            /// There is client authentication
            /// </summary>
            Client
        }

        /// <summary>
        /// Current server bind port
        /// </summary>
        private int Port => this.Sys.Settings.Config.GetInt("KlusterKite.Web.WebHostPort");

        /*
        
        /// <summary>
        /// Checks various authorization combinations
        /// </summary>
        /// <param name="authenticationType">The authentication type</param>
        /// <param name="method">The requested method</param>
        /// <param name="expectedResult">The expected response code</param>
        /// <returns>The async task</returns>
        [Theory]
        [InlineData(EnAuthenticationType.None, "session", HttpStatusCode.Unauthorized)]
        [InlineData(EnAuthenticationType.User, "session", HttpStatusCode.OK)]
        [InlineData(EnAuthenticationType.Client, "session", HttpStatusCode.OK)]
        [InlineData(EnAuthenticationType.None, "user", HttpStatusCode.Unauthorized)]
        [InlineData(EnAuthenticationType.User, "user", HttpStatusCode.OK)]
        [InlineData(EnAuthenticationType.Client, "user", HttpStatusCode.Unauthorized)]
        [InlineData(EnAuthenticationType.User, "AuthorizedUserAction", HttpStatusCode.OK)]
        [InlineData(EnAuthenticationType.Client, "AuthorizedUserAction", HttpStatusCode.Unauthorized)]
        [InlineData(EnAuthenticationType.User, "UnauthorizedUserAction", HttpStatusCode.Unauthorized)]
        [InlineData(EnAuthenticationType.Client, "UnauthorizedUserAction", HttpStatusCode.Unauthorized)]
        [InlineData(EnAuthenticationType.User, "UnauthorizedClientUserAction", HttpStatusCode.Unauthorized)]
        [InlineData(EnAuthenticationType.Client, "UnauthorizedClientUserAction", HttpStatusCode.Unauthorized)]
        [InlineData(EnAuthenticationType.User, "AuthorizedEitherClientUserAction", HttpStatusCode.OK)]
        [InlineData(EnAuthenticationType.Client, "AuthorizedEitherClientUserAction", HttpStatusCode.OK)]
        [InlineData(EnAuthenticationType.User, "authorizedUserExactAction", HttpStatusCode.OK)]
        [InlineData(EnAuthenticationType.User, "unauthorizedUserExactAction", HttpStatusCode.Unauthorized)]
        public async Task CheckAuthorization(EnAuthenticationType authenticationType, string method, HttpStatusCode expectedResult) 
        {
            this.ExpectNoMsg();

            var client = new RestClient($"http://localhost:{this.Port}/authorization/test") { Timeout = 5000 };
            switch (authenticationType)
            {
                case EnAuthenticationType.User:
                    client.Authenticator = new OAuth2AuthorizationRequestHeaderAuthenticator(await this.SetUserSession(), "Bearer");
                    break;
                case EnAuthenticationType.Client:
                    client.Authenticator = new OAuth2AuthorizationRequestHeaderAuthenticator(await this.SetClientSession(), "Bearer");
                    break;
            }

            var request = new RestRequest { Method = Method.GET, Resource = method };
            request.AddHeader("Accept", "application/json, text/json");
            var result = await client.ExecuteTaskAsync(request);

            Assert.Equal(expectedResult, result.StatusCode);
        }

        /// <summary>
        /// In case of invalid token, controller should return 401
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task ForbiddenOnInvalidToken()
        {
            this.ExpectNoMsg();
            var client = new RestClient($"http://localhost:{this.Port}/authorization/public") { Timeout = 5000 };
            client.Authenticator = new OAuth2AuthorizationRequestHeaderAuthenticator(
                Guid.NewGuid().ToString("N"),
                "Bearer");

            var request = new RestRequest { Method = Method.GET, Resource = "test" };
            request.AddHeader("Accept", "application/json, text/json");
            var result = await client.ExecuteTaskAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        }

        /// <summary>
        /// In case of invalid token, controller should return 401
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task ValidOnNoToken()
        {
            this.ExpectNoMsg();
            var client = new RestClient($"http://localhost:{this.Port}/authorization/public") { Timeout = 5000 };
           
            var request = new RestRequest { Method = Method.GET, Resource = "test" };
            request.AddHeader("Accept", "application/json, text/json");
            var result = await client.ExecuteTaskAsync(request);

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        /// <summary>
        /// Sets the user authenticated session
        /// </summary>
        /// <returns>The user token</returns>
        private async Task<string> SetUserSession()
        {
            var session = new AccessTicket(
                new User { UserId = "testUser" },
                new[] { "User1", "User.AuthorizedUserExactAction" },
                "testClient",
                "testClientType",
                new[] { "Client1", "Client2", "Client.AuthorizedUserExactAction" },
                DateTimeOffset.Now,
                DateTimeOffset.Now.AddMinutes(1),
                "hello world");

            return await this.Container.Resolve<ITokenManager>().CreateAccessToken(session);
        }

        /// <summary>
        /// Sets the client authenticated session
        /// </summary>
        /// <returns>The user token</returns>
        private async Task<string> SetClientSession()
        {
            var session = new AccessTicket(
                null,
                null,
                "testClient-2",
                "testClientType",
                new[] { "Client1-2", "Client2-2" },
                DateTimeOffset.Now,
                DateTimeOffset.Now.AddMinutes(1),
                "hello world");

            return await this.Container.Resolve<ITokenManager>().CreateAccessToken(session);
        }
        */

        /// <summary>
        /// The test configurator
        /// </summary>
        public class Configurator : TestConfigurator
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

                // todo: move connection string to external config or environment
                return ConfigurationFactory.ParseString($@"
                {{
                    KlusterKite {{
 		                Web {{
                            Authentication.RedisConnection = ""docker:6379""
                            WebHostPort = {port},
 			                BindAddress = ""http://*:{port}"",
                            Debug.Trace = true
                        }}
                    }}
                }}
").WithFallback(base.GetAkkaConfig(containerBuilder));
            }

            /// <summary>
            /// Gets list of all used plugin installers
            /// </summary>
            /// <returns>The list of installers</returns>
            public override List<BaseInstaller> GetPluginInstallers()
            {
                var installers = base.GetPluginInstallers();
                installers.Add(new Descriptor.Installer());
                installers.Add(new Web.Installer());
                // ReSharper disable once RedundantNameQualifier
                installers.Add(new Web.Authorization.Installer());
                installers.Add(new TestInstaller());
                return installers;
            }
        }

        

        /// <summary>
        /// The test installer to register components
        /// </summary> 
        public class TestInstaller : BaseInstaller
        {
            /// <inheritdoc />
            protected override decimal AkkaConfigLoadPriority => PriorityClusterRole;

            /// <inheritdoc />
            protected override Config GetAkkaConfig()
            {
                return Config.Empty;
            }

            /// <inheritdoc />
            protected override void RegisterComponents(ContainerBuilder container, Config config)
            {
                container.RegisterType<TestController>();
                container.RegisterType<PublicTestController>();
                container.RegisterType<MoqTokenManager>().As<ITokenManager>().SingleInstance();
            }
        }

        /// <summary>
        /// The test user
        /// </summary>
        private class User : IUser
        {
            /// <inheritdoc />
            public string UserId { get; set; }
        }
    }
}