// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DataActorTests.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Testing the <see cref="BaseCrudActor{TContext}" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using Akka.Event;
    using Akka.Actor;
    using Akka.Configuration;
    using Akka.DI.Core;

    using Autofac;

    using KlusterKite.API.Client;
    using KlusterKite.Core;
    using KlusterKite.Core.TestKit;
    using KlusterKite.Data.CRUD.ActionMessages;
    using KlusterKite.Data.EF;
    using KlusterKite.Data.Tests.Mock;

    using Newtonsoft.Json.Linq;

    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Testing the <see cref="BaseCrudActor{TContext}"/>
    /// </summary>
    public class DataActorTests : BaseActorTest<DataActorTests.Configurator>
    {
        /// <inheritdoc />
        public DataActorTests(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// Testing the create request
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task CreateTest()
        {
            var actor = this.InitContext();

            var uid = Guid.Parse("{72C23018-0C49-4419-8982-D7C0168E8DC3}");
            var request =
                new CrudActionMessage<User, Guid>
                    {
                        ActionType = EnActionType.Create,
                        Data = new User
                                   {
                                       Login = "new_user",
                                       Password = "456",
                                       Uid = uid
                                   }
                    };

            var result = await actor.Ask<CrudActionResponse<User>>(request, TimeSpan.FromSeconds(5));
            Assert.NotNull(result);
            if (result.Exception != null)
            {
                this.Sys.Log.Error(result.Exception, "Exception");
            }

            Assert.Null(result.Exception);
            Assert.NotNull(result.Data);
            Assert.Equal("new_user", result.Data.Login);

            var contextFactory = this.Container.Resolve<UniversalContextFactory>();
            using (var context = contextFactory.CreateContext<TestDataContext>("InMemory", null, "test"))
            {
                Assert.Equal(3, context.Users.Count());
            }
        }

        /// <summary>
        /// Testing the delete request
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task DeleteTest()
        {
            var actor = this.InitContext();

            var uid = Guid.Parse("{72C23018-0C49-4419-8982-D7C0168E8DC2}");
            var request = new CrudActionMessage<User, Guid> { ActionType = EnActionType.Delete, Id = uid };

            var result = await actor.Ask<CrudActionResponse<User>>(request, TimeSpan.FromSeconds(5));
            Assert.NotNull(result);
            if (result.Exception != null)
            {
                this.Sys.Log.Error(result.Exception, "Exception");
            }

            Assert.Null(result.Exception);
            Assert.NotNull(result.Data);
            Assert.Equal("user1", result.Data.Login);

            var contextFactory = this.Container.Resolve<UniversalContextFactory>();
            using (var context = contextFactory.CreateContext<TestDataContext>("InMemory", null, "test"))
            {
                Assert.Equal(1, context.Users.Count());
            }
        }

        /// <summary>
        /// Testing the get by id request
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task GetByIdTest()
        {
            var actor = this.InitContext();

            var request =
                new CrudActionMessage<User, Guid>
                    {
                        ActionType = EnActionType.Get,
                        Id = Guid.Parse("{72C23018-0C49-4419-8982-D7C0168E8DC2}")
                    };

            var result = await actor.Ask<CrudActionResponse<User>>(request, TimeSpan.FromSeconds(5));
            Assert.NotNull(result);
            if (result.Exception != null)
            {
                this.Sys.Log.Error(result.Exception, "Exception");
            }

            Assert.Null(result.Exception);
            Assert.NotNull(result.Data);
            Assert.Equal("user1", result.Data.Login);
        }

        /// <summary>
        /// Testing update request
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task UpdateTest()
        {
            var actor = this.InitContext();

            var uid = Guid.Parse("{72C23018-0C49-4419-8982-D7C0168E8DC2}");
            var request =
                new CrudActionMessage<User, Guid>
                    {
                        ActionType = EnActionType.Update,
                        Id = uid,
                        Data = new User
                                   {
                                       Login = "new_user",
                                       Password = "456",
                                       Uid = uid
                                   }
                    };

            var result = await actor.Ask<CrudActionResponse<User>>(request, TimeSpan.FromSeconds(5));
            Assert.NotNull(result);
            if (result.Exception != null)
            {
                this.Sys.Log.Error(result.Exception, "Exception");
            }

            Assert.Null(result.Exception);
            Assert.NotNull(result.Data);
            Assert.Equal("new_user", result.Data.Login);
            Assert.Equal("456", result.Data.Password);

            var contextFactory = this.Container.Resolve<UniversalContextFactory>();
            using (var context = contextFactory.CreateContext<TestDataContext>("InMemory", null, "test"))
            {
                var user = context.Users.FirstOrDefault(u => u.Uid == uid);
                Assert.NotNull(user);
                Assert.Equal("new_user", user.Login);
                Assert.Equal("456", user.Password);
            }
        }

        /// <summary>
        /// Testing update with api request
        /// </summary>
        /// <returns>The async task</returns>
        [Fact]
        public async Task UpdateWithApiRequestTest()
        {
            var actor = this.InitContext();

            var uid = Guid.Parse("{72C23018-0C49-4419-8982-D7C0168E8DC2}");
            var argumentsJson = @"
                {
                    ""id"": ""72C23018-0C49-4419-8982-D7C0168E8DC2"",
                    ""newNode"": {
                        ""login"": ""new_user""                     
                    }
                }
            ";
            var apiRequest = new ApiRequest { Arguments = JObject.Parse(argumentsJson) };

            var request =
                new CrudActionMessage<User, Guid>
                    {
                        ActionType = EnActionType.Update,
                        Id = uid,
                        Data = new User
                                   {
                                       Login = "new_user",
                                       Password = "456",
                                       Uid = uid
                                   },
                        ApiRequest = apiRequest
                    };

            var result = await actor.Ask<CrudActionResponse<User>>(request, TimeSpan.FromSeconds(5));
            Assert.NotNull(result);
            if (result.Exception != null)
            {
                this.Sys.Log.Error(result.Exception, "Exception");
            }

            Assert.Null(result.Exception);
            Assert.NotNull(result.Data);
            Assert.Equal("new_user", result.Data.Login);
            Assert.Equal("123", result.Data.Password);

            var contextFactory = this.Container.Resolve<UniversalContextFactory>();
            using (var context = contextFactory.CreateContext<TestDataContext>("InMemory", null, "test"))
            {
                var user = context.Users.FirstOrDefault(u => u.Uid == uid);
                Assert.NotNull(user);
                Assert.Equal("new_user", user.Login);
                Assert.Equal("123", user.Password);
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            var contextFactory = this.Container.Resolve<UniversalContextFactory>();
            using (var context = contextFactory.CreateContext<TestDataContext>("InMemory", null, "test"))
            {
                context.Database.EnsureDeleted();
            }
        }

        /// <summary>
        /// Performs context initialization
        /// </summary>
        /// <returns>The data actor</returns>
        private IActorRef InitContext()
        {
            var contextFactory = this.Container.Resolve<UniversalContextFactory>();
            using (var context = contextFactory.CreateContext<TestDataContext>("InMemory", null, "test"))
            {
                var user1 = new User
                                {
                                    Login = "user1",
                                    Password = "123",
                                    Uid = Guid.Parse("{72C23018-0C49-4419-8982-D7C0168E8DC2}")
                                };

                var user2 = new User
                                {
                                    Login = "user2",
                                    Password = "123",
                                    Uid = Guid.Parse("{D906C0AB-1108-4B39-B179-C30C83425482}")
                                };

                context.Users.Add(user1);
                context.Users.Add(user2);
                context.SaveChanges();
            }

            var actor = this.Sys.ActorOf(this.Sys.DI().Props<TestDataActor>(), "data");
            this.ExpectNoMsg();
            return actor;
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
                var pluginInstallers =
                    new List<BaseInstaller>
                        {
                            new Core.Installer(),
                            new Core.TestKit.Installer(),
                            new TestInstaller(),
                            new Data.Installer(),
                            new EF.Installer(),
                            new EF.InMemory.Installer(),
                        };
                return pluginInstallers;
            }
        }

        /// <summary>
        /// Replaces production data sources with the test ones
        /// </summary>
        private class TestInstaller : BaseInstaller
        {
            /// <summary>
            /// Gets priority for ordering akka configurations. Highest priority will override lower priority.
            /// </summary>
            /// <remarks>Consider using <seealso cref="BaseInstaller"/> integrated constants</remarks>
            protected override decimal AkkaConfigLoadPriority => -1M;

            /// <summary>
            /// Gets default akka configuration for current module
            /// </summary>
            /// <returns>Akka configuration</returns>
            protected override Config GetAkkaConfig() => ConfigurationFactory.Empty;

            /// <inheritdoc />
            protected override void RegisterComponents(ContainerBuilder container, Config config)
            {
                container.RegisterInstance(new DatabaseInstanceName());
                container.RegisterAssemblyTypes(typeof(TestInstaller).GetTypeInfo().Assembly).Where(t => t.GetTypeInfo().IsSubclassOf(typeof(ActorBase)));
                container.RegisterType<TestUserFactory>().As<DataFactory<TestDataContext, User, Guid>>();
                container.RegisterType<TestRolesFactory>().As<DataFactory<TestDataContext, Role, Guid>>();
            }
        }
    }
}