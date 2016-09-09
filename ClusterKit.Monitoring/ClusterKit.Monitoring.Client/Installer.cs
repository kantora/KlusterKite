﻿namespace ClusterKit.Monitoring.Client
{
    using System.Collections.Generic;
    using System.Web.Http;
    using System.Web.Mvc;

    using Akka.Actor;
    using Akka.Configuration;

    using Castle.MicroKernel.Registration;
    using Castle.MicroKernel.SubSystems.Configuration;
    using Castle.Windsor;

    using ClusterKit.Core;

    /// <summary>
    /// Installing components from current library
    /// </summary>
    public class Installer : BaseInstaller
    {
        /// <summary> 
        /// Gets priority for ordering akka configurations. Highest priority will override lower priority.
        /// </summary>
        /// <remarks>Consider using <seealso cref="BaseInstaller"/> integrated constants</remarks>
        protected override decimal AkkaConfigLoadPriority => PriorityClusterRole;

        /// <summary>
        /// Gets default akka configuration for current module
        /// </summary>
        /// <returns>Akka configuration</returns>
        protected override Config GetAkkaConfig() => ConfigurationFactory.ParseString(Configuration.AkkaConfig);

        /// <summary>
        /// Gets list of roles, that would be assign to cluster node with this plugin installed.
        /// </summary>
        /// <returns>The list of roles</returns>
        protected override IEnumerable<string> GetRoles() => new[] { "Monitoring" };

        /// <summary>
        /// Registering DI components
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="store">The configuration store.</param>
        protected override void RegisterWindsorComponents(IWindsorContainer container, IConfigurationStore store)
        {
            container.Register(
                Classes.FromThisAssembly().Where(t => t.IsSubclassOf(typeof(ActorBase))).LifestyleTransient());
            container.Register(Classes.FromThisAssembly().Where(t => t.IsSubclassOf(typeof(ApiController))).LifestyleTransient());
        }
    }
}