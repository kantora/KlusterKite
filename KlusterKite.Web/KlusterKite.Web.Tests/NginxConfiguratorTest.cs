// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NginxConfiguratorTest.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Testing work of <seealso cref="NginxConfiguratorActor" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.Tests
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;

    using Akka.Cluster;
    using Akka.Configuration;
    using Akka.DI.Core;
    using Akka.Event;
    using Autofac;

    using KlusterKite.Core;
    using KlusterKite.Core.TestKit;
    using KlusterKite.Web.Client.Messages;
    using KlusterKite.Web.NginxConfigurator;

    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Testing work of <seealso cref="NginxConfiguratorActor"/>
    /// </summary>
    public class NginxConfiguratorTest : BaseActorTest<NginxConfiguratorTest.Configurator>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NginxConfiguratorTest"/> class.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        public NginxConfiguratorTest(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// Testing generation of nginx.config
        /// </summary>
        [Fact]
        public void ServiceConfigGenerationTest()
        {
            // BaseInstaller.RunPreCheck(this.Container, this.Sys.Settings.Config);
            var configurator = this.ActorOfAsTestActorRef<NginxConfiguratorActor>("configurator");

            this.ActorOfAsTestActorRef<NameSpaceActor>(this.Sys.DI().Props<NameSpaceActor>(), "Web");

            var address = Cluster.Get(this.Sys).SelfAddress;
            configurator.Tell(
                new ClusterEvent.MemberUp(
                    ClusterExtensions.MemberCreate(new UniqueAddress(address, 1), 1, MemberStatus.Up, ImmutableHashSet.Create("Web"))));
            this.ExpectTestMsg<WebDescriptionRequest>();

            Assert.Single(configurator.UnderlyingActor.KnownActiveNodes);

            configurator.Tell(
                new WebDescriptionResponse
                {
                    Services =
                            new List<ServiceDescription>
                                {
                                    new ServiceDescription
                                        {
                                            ListeningPort = 8080,
                                            PublicHostName = "web1",
                                            Route = "/TestWebService"
                                        },
                                    new ServiceDescription
                                        {
                                            ListeningPort = 8080,
                                            PublicHostName = "web1",
                                            Route = "/test/TestWebService2"
                                        },
                                    new ServiceDescription
                                        {
                                            ListeningPort = 8080,
                                            PublicHostName = "web2",
                                            Route = "/Api"
                                        }
                                }
                            .AsReadOnly()
                });

            Assert.Equal(1, configurator.UnderlyingActor.NodePublishUrls.Count);
            Assert.Equal(
                "0.0.0.0:8080",
                configurator.UnderlyingActor.Configuration["web1"]["/TestWebService"].ActiveNodes[0].NodeUrl);
            Assert.Equal(
                "0.0.0.0:8080",
                configurator.UnderlyingActor.Configuration["web1"]["/test/TestWebService2"].ActiveNodes[0].NodeUrl);
            Assert.Equal(
                "0.0.0.0:8080",
                configurator.UnderlyingActor.Configuration["web2"]["/Api"].ActiveNodes[0].NodeUrl);

            var config = File.ReadAllText("./nginx.conf");
            this.Sys.Log.Log(LogLevel.InfoLevel, config);
        }

        /// <summary>
        /// The test configurator
        /// </summary>
        public class Configurator : TestConfigurator
        {
            /// <inheritdoc />
            public override Config GetAkkaConfig(ContainerBuilder containerBuilder)
            {
                return ConfigurationFactory.ParseString(@"
                {
                    KlusterKite {
	 		                    Web {
	 			                    Nginx {
	 				                    PathToConfig = ""./nginx.conf""
                                        Configuration {
                                            web1 {
                                               listen: 8081
                                               ""location /api/1.x/external"" {
                                                    proxy_pass = ""http://external/api/1.x/external""
                                               }
                                            }
                                            web2 {
                                               listen: 8082
                                               server_name: ""www.example.com""
                                               ""location /"" {
                                                         root = /var/www/example/
                                                }
                                            }
                                           ""web.3"" {
                                             server_name: ""www.example2.com""
                                           }
                                        }
                                }
                            }
                        }

            akka.actor {
                        serializers {
                            hyperion = ""Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion""
                        }
                        serialization-bindings {
                            ""System.Object"" = hyperion
                        }
                    }

            akka.actor.deployment {
                    /Web {
                        IsNameSpace = true
                        dispatcher = akka.test.calling-thread-dispatcher
                    }

                    /Web/Descriptor {
                        type = ""KlusterKite.Core.TestKit.TestActorForwarder, KlusterKite.Core.TestKit""
                        dispatcher = akka.test.calling-thread-dispatcher
                    }
                }

                }").WithFallback(base.GetAkkaConfig(containerBuilder));
            }

            /// <summary>
            /// Gets list of all used plugin installers
            /// </summary>
            /// <returns>The list of installers</returns>
            public override List<BaseInstaller> GetPluginInstallers()
            {
                var installers = base.GetPluginInstallers();
                installers.Add(new NginxConfigurator.Installer());
                return installers;
            }
        }
    }
}