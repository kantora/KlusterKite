﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NginxConfiguratorTest.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   Testing work of <seealso cref="NginxConfiguratorActor" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.Web.Tests
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;

    using Akka.Actor;
    using Akka.Cluster;
    using Akka.Configuration;
    using Akka.DI.Core;
    using Akka.TestKit;

    using Castle.Windsor;

    using ClusterKit.Core;
    using ClusterKit.Core.TestKit;
    using ClusterKit.Web.Client.Messages;
    using ClusterKit.Web.NginxConfigurator;

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
            BaseInstaller.RunPrecheck(this.WindsorContainer, this.Sys.Settings.Config);
            var configurator = this.ActorOfAsTestActorRef<NginxConfiguratorActor>("configurator");
            var webNamespace =
                this.ActorOfAsTestActorRef<NginxConfiguratorActor>(this.Sys.DI().Props<TestActorForwarder>(), "Web");
            var webDescriptor =
                webNamespace.Ask<IActorRef>(
                    new TestActorForwarder.CreateChildMessage()
                    {
                        Props = this.Sys.DI().Props<TestActorForwarder>(),
                        Name = "Descriptor"
                    }).Result;

            var address = Cluster.Get(this.Sys).SelfAddress;
            configurator.Tell(
                new ClusterEvent.MemberUp(
                    Member.Create(new UniqueAddress(address, 1), MemberStatus.Up, ImmutableHashSet.Create("Web"))));
            this.ExpectTestMsg<WebDescriptionRequest>();

            Assert.Equal(1, configurator.UnderlyingActor.KnownActiveNodes.Count);

            configurator.Tell(
                new WebDescriptionResponse
                {
                    ListeningPort = 8080,
                    ServiceNames = new Dictionary<string, string>
                                           {
                                               { "/TestWebService", "default" },
                                               { "/test/TestWebService2", "default" },
                                               { "/Api", "web" }
                                           }
                },
                webDescriptor);

            Assert.Equal(1, configurator.UnderlyingActor.NodePublishUrls.Count);
            Assert.Equal("127.0.0.1:8080", configurator.UnderlyingActor.NodePublishUrls.First().Value);
            Assert.Equal("127.0.0.1:8080", configurator.UnderlyingActor.Configuration["default"]["/TestWebService"].ActiveNodes[0]);
            Assert.Equal("127.0.0.1:8080", configurator.UnderlyingActor.Configuration["default"]["/test/TestWebService2"].ActiveNodes[0]);
            Assert.Equal("127.0.0.1:8080", configurator.UnderlyingActor.Configuration["web"]["/Api"].ActiveNodes[0]);

            var config = File.ReadAllText("./nginx.conf");
            this.Sys.Log.Info(config);
        }

        /// <summary>
        /// The test configurator
        /// </summary>
        public class Configurator : TestConfigurator
        {
            /// <summary>
            /// Gets the akka system config
            /// </summary>
            /// <param name="windsorContainer">
            /// The windsor Container.
            /// </param>
            /// <returns>
            /// The config
            /// </returns>
            public override Config GetAkkaConfig(IWindsorContainer windsorContainer)
            {
                return ConfigurationFactory.ParseString(@"
                {
                    ClusterKit {
	 		                    Web {
	 			                    Nginx {
	 				                    PathToConfig = ""./nginx.conf""
                                        Configuration {
                                            default {
                                               listen: 80
                                            }
                                            web {
                                               listen: 8080
                                               server_name: ""www.example.com""
                                               ""location /"" {
                                                         root = /var/www/example/
                                                }
                                            }
                                        }
                                }
                            }
                        }

                    akka.actor.deployment {
                        ""/*"" {
                           dispatcher = ClusterKit.test-dispatcher
                        }
                        ""/*/*"" {
                           dispatcher = ClusterKit.test-dispatcher
                        }
                        ""/*/*/*"" {
                           dispatcher = ClusterKit.test-dispatcher
                        }
                    }
                }").WithFallback(base.GetAkkaConfig(windsorContainer));
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