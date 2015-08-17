﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Bootstrapper.cs" company="TaxiKit">
//   All rights reserved
// </copyright>
// <summary>
//   Dependency injection configuration
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace TaxiKit.Core.Service
{
    using System.Configuration;

    using Akka.Actor;
    using Akka.Configuration.Hocon;
    using Akka.DI.CastleWindsor;
    using Akka.DI.Core;

    using Castle.Facilities.TypedFactory;
    using Castle.MicroKernel.Registration;
    using Castle.MicroKernel.Resolvers.SpecializedResolvers;
    using Castle.Windsor;

    using CommonServiceLocator.WindsorAdapter;

    using Microsoft.Practices.ServiceLocation;

    using StackExchange.Redis;

    /// <summary>
    /// Dependency injection configuration
    /// </summary>
    public class Bootstrapper
    {
        /// <summary>
        ///  Dependency injection configuration
        /// </summary>
        /// <param name="container">Dependency injection container</param>
        public static void Configure(IWindsorContainer container)
        {
            container.AddFacility<TypedFactoryFacility>();
            container.Kernel.Resolver.AddSubResolver(new ArrayResolver(container.Kernel, true));
            container.Register(Component.For<Controller>().LifestyleTransient());

            // registering redis connection
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(ConfigurationManager.ConnectionStrings["redis"].ConnectionString);
            container.Register(
                Component
                .For<IConnectionMultiplexer>()
                .Instance(redis).LifestyleSingleton());

            container.RegisterWindsorInstallers();

            // starting akka system
            var config = BaseInstaller.GetStackedConfig(container);
            var actorSystem = ActorSystem.Create("TaxiKit", config);
            actorSystem.AddDependencyResolver(new WindsorDependencyResolver(container, actorSystem));
            actorSystem.StartNameSpaceActorsFromConfiguration();

            container.Register(Component.For<ActorSystem>().Instance(actorSystem).LifestyleSingleton());
            ServiceLocator.SetLocatorProvider(() => new WindsorServiceLocator(container));
        }
    }
}