// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ControllerTests.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Tests that ApiControllers are working
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.Tests
{
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    using Akka.Configuration;
    using Akka.Event;
    using Autofac;

    using KlusterKite.Core;
    using KlusterKite.Core.TestKit;

    using RestSharp;
    
    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Tests that ApiControllers are working
    /// </summary>
    public class ControllerTests : WebTest<ControllerTests.Configurator>
    {
        /// <inheritdoc />
        public ControllerTests(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// Current web server bind port
        /// </summary>
        private int Port => this.Sys.Settings.Config.GetInt("KlusterKite.Web.WebHostPort");

        /// <summary>
        /// Testing published controller method
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task RequestTest()
        {
            this.ExpectNoMsg();

            var options = new RestClientOptions($"http://localhost:{this.Port}/testController") { Timeout = new System.TimeSpan(0,0,5) };
            var client = new RestClient(options);
            var request = new RestRequest { Method = Method.Get, Resource = "method" };
            request.AddHeader("Accept", "application/json, text/json");
            var result = await client.ExecuteAsync(request);

            Assert.Equal(ResponseStatus.Completed, result.ResponseStatus);
            this.Sys.Log.Log(LogLevel.InfoLevel, "Response: {Response}", [result.Content]);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal("\"Hello world\"", result.Content);
        }

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

                return ConfigurationFactory.ParseString(
                        $@"
                {{
                    KlusterKite {{
 		                Web {{
                            WebHostPort = {port},
 			                BindAddress = ""http://*:{port}"",
                            Debug.Trace = true
                        }}
                    }}
                }}")
                    .WithFallback(base.GetAkkaConfig(containerBuilder));
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
            }
        }
    }
}