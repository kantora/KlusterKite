// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationTest.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace KlusterKite.Core.Tests.Configuration
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using Akka.Actor;
    using Akka.Cluster.Sharding;
    using Akka.Configuration;

    using Autofac;

    using JetBrains.Annotations;

    using KlusterKite.Core.TestKit;

    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Global configuration test
    /// </summary>
    public class ConfigurationTest : BaseActorTest<ConfigurationTest.Configurator>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationTest"/> class.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        public ConfigurationTest(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// Testing that hocon config stacks as expected
        /// </summary>
        [Fact]
        public void CheckConfigStacking()
        {
            var config1 = @"{
                akka.actor.deployment {
                    /testRoot1 {
                        autoDeploy = on
                    }
                 }
            }";

            var config2 = @"{
                akka.actor.deployment {
                    /testRoot2 {
                        autoDeploy = on
                    }
                 }
            }";

            var endConfig = ConfigurationFactory.Empty
                .WithFallback(config1)
                .WithFallback(config2);

            var deployConfig = endConfig.GetConfig("akka.actor.deployment");
            Assert.Equal(2, deployConfig.AsEnumerable().Count());
            Assert.True(endConfig.GetBoolean("akka.actor.deployment./testRoot1.autoDeploy"));
            Assert.True(endConfig.GetBoolean("akka.actor.deployment./testRoot2.autoDeploy"));
        }

        /// <summary>
        /// Testing correct <seealso cref="NameSpaceActor"/> work
        /// </summary>
        [Fact]
        public void NameSpaceActorTest()
        {
            // todo: fix test cluster joining and add all types of actors to test
            /*
            var cluster = Akka.Cluster.Cluster.Get(this.Sys);
            cluster.Join(cluster.SelfAddress);
            //*/

            this.Sys.StartNameSpaceActorsFromConfiguration();
            this.ExpectNoMsg();
            this.Sys.ActorSelection("/user/testNameSpace/forwarder").Tell("Hello world");
            Assert.Equal("Hello world", this.ExpectMsg<string>("/user/testNameSpace/forwarder"));
            this.Sys.ActorSelection("/user/testNameSpace/second/forwarder").Tell("Hello world");
            Assert.Equal("Hello world", this.ExpectMsg<string>("/user/testNameSpace/second/forwarder"));

            /*
            var shardingActor = this.Sys.ActorSelection("/user/testNameSpace/sharding");
            var shardingState = await shardingActor.Ask<ClusterShardingStats>(
                new GetClusterShardingStats(TimeSpan.FromMilliseconds(100)),
                TimeSpan.FromMilliseconds(2000));

            shardingActor.Tell("Hello sharding world");
            var shardMessage = this.ExpectMsg<TestMessage<string>>();
            Assert.Equal("Hello sharding world", shardMessage.Message);
            this.Sys.Log.Info(shardMessage.ReceiverPath);
            */
        }

        /// <summary>
        /// The current test configuration
        /// </summary>
        public class Configurator : TestConfigurator
        {
            /// <inheritdoc />
            public override Config GetAkkaConfig(ContainerBuilder containerBuilder)
            {
                return ConfigurationFactory.ParseString(@"{
                akka.actor.deployment {
                    /testNameSpace {
                        IsNameSpace = true
                        dispatcher = akka.test.calling-thread-dispatcher
                    }

                    /somethingElse {
                    }

                    /testNameSpace/forwarder {
                        type = ""KlusterKite.Core.TestKit.TestActorForwarder, KlusterKite.Core.TestKit""
                        dispatcher = akka.test.calling-thread-dispatcher
                    }

                    /testNameSpace/second {
                        type = ""KlusterKite.Core.NameSpaceActor, KlusterKite.Core""
                        dispatcher = akka.test.calling-thread-dispatcher
                    }

                    /testNameSpace/second/forwarder {
                        type = ""KlusterKite.Core.TestKit.TestActorForwarder, KlusterKite.Core.TestKit""
                        dispatcher = akka.test.calling-thread-dispatcher
                    }

                    /testNameSpace/sharding {
                        actor-type = Sharding
                        type-name = test-shard
                        type = ""KlusterKite.Core.TestKit.TestActorForwarder, KlusterKite.Core.TestKit""
                        role = test
                        message-extractor = ""KlusterKite.Core.Tests.Configuration.ConfigurationTest+TestMessageExtractor, KlusterKite.Core.Tests""
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
                var pluginInstallers = base.GetPluginInstallers();
                pluginInstallers.Add(new TestInstaller());
                return pluginInstallers;
            }
        }

        /// <summary>
        /// The test installer
        /// </summary>
        public class TestInstaller : BaseInstaller
        {
            /// <inheritdoc />
            protected override decimal AkkaConfigLoadPriority => -1M;

            /// <inheritdoc />
            protected override Config GetAkkaConfig() => ConfigurationFactory.Empty;

            /// <inheritdoc />
            protected override IEnumerable<string> GetRoles() => new[]
                                                                     {
                                                                         "test"
                                                                     };

            /// <inheritdoc />
            protected override void RegisterComponents(ContainerBuilder container, Config config)
            {
                container.RegisterAssemblyTypes(typeof(Installer).GetTypeInfo().Assembly).Where(t => t.GetTypeInfo().IsSubclassOf(typeof(ActorBase)));
                container.RegisterAssemblyTypes(typeof(TestMessageExtractor).GetTypeInfo().Assembly);
            }
        }

        /// <summary>
        /// Test message extractor
        /// </summary>
        [UsedImplicitly]
        public class TestMessageExtractor : IMessageExtractor
        {
            /// <inheritdoc />
            public string EntityId(object message) => message.GetType().Name;

            /// <inheritdoc />
            public object EntityMessage(object message) => message;

            /// <inheritdoc />
            public string ShardId(object message) => message.GetType().GetTypeInfo().Assembly.FullName;
        }
    }
}