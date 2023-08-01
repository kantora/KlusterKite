// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Installer.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Installing components from current library
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace KlusterKite.Web
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    
    using Akka.Actor;
    using Akka.Event;
    using Akka.Configuration;

    using Autofac;

    using KlusterKite.Core;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// Installing components from current library
    /// </summary>
    public class Installer : BaseInstaller
    {
        /// <summary>
        /// The service task
        /// </summary>
        private readonly CancellationTokenSource service = new CancellationTokenSource();

        /// <summary>
        /// Gets priority for ordering akka configurations. Highest priority will override lower priority.
        /// </summary>
        /// <remarks>Consider using <seealso cref="BaseInstaller"/> integrated constants</remarks>
        protected override decimal AkkaConfigLoadPriority => PrioritySharedLib;

        /// <summary>
        /// Gets default akka configuration for current module
        /// </summary>
        /// <returns>Akka configuration</returns>
        protected override Config GetAkkaConfig() => ConfigurationFactory.ParseString(ReadTextResource(typeof(Installer).GetTypeInfo().Assembly, "KlusterKite.Web.Resources.akka.hocon"));

        /// <summary>
        /// Gets list of roles, that would be assign to cluster node with this plugin installed.
        /// </summary>
        /// <returns>The list of roles</returns>
        protected override IEnumerable<string> GetRoles() => new[]
                                                                 {
                                                                     "Web"
                                                                 };

        /// <inheritdoc />
        protected override void RegisterComponents(ContainerBuilder container, Config config)
        {
            container.RegisterAssemblyTypes(typeof(Installer).GetTypeInfo().Assembly).Where(t => t.GetTypeInfo().IsSubclassOf(typeof(ActorBase)));
            container.RegisterType<WebTracer>().As<IWebHostingConfigurator>();

            var registeredAssemblies =
                GetRegisteredBaseInstallers(container)
                    .Select(i => i.GetType().GetTypeInfo().Assembly)
                    .Distinct();

            foreach (var registeredAssembly in registeredAssemblies)
            {
                container.RegisterAssemblyTypes(registeredAssembly).Where(t => t.GetTypeInfo().IsSubclassOf(typeof(Controller)));
            }

            Startup.ContainerBuilder = container;
            Startup.Config = config;
            var bindUrl = GetWebHostingBindUrl(config);
            var host = new WebHostBuilder()
                .CaptureStartupErrors(true)
                .UseUrls(bindUrl)
                .UseKestrel();

            Task.Run(
                async () =>
                    {
                        var server = host.UseStartup<Startup>().Build();
                        var system = (await Startup.ContainerWaiter.Task).Resolve<ActorSystem>();
                        try
                        {
                            system.Log.Info("Starting web server...");
                            server.Run();
                        }
                        catch (Exception exception)
                        {
                            system.Log.Error(exception, "Web server stopped");
                        }
                    }, 
                this.service.Token);

            var timeout = config.GetTimeSpan("KlusterKite.Web.InitializationTimeout", TimeSpan.FromSeconds(15));
            if (!Startup.ServiceConfigurationWaiter.Wait(timeout))
            {
                throw new Exception("Web server initialization timeout", Startup.LastException);
            }
        }

        /// <inheritdoc />
        protected override void PostStart(IComponentContext context)
        {
            var actorSystem = context.Resolve<ActorSystem>();
            actorSystem.RegisterOnTermination(() => this.service.Cancel());
            actorSystem.Log.Info("{Type}: post start started", this.GetType().FullName);
            Startup.ContainerWaiter.SetResult(context);
            var timeout = actorSystem.Settings.Config.GetTimeSpan("KlusterKite.Web.InitializationTimeout", TimeSpan.FromSeconds(15));
            if (!Startup.ServiceStartWaiter.Wait(timeout))
            {
                throw new Exception("Web server start timeout", Startup.LastException);
            }

            actorSystem.Log.Info("{Type}: post start completed", this.GetType().FullName);
        }

        /// <summary>
        /// Reads bind url from configuration
        /// </summary>
        /// <param name="config">The akka config</param>
        /// <returns>The Url to bind web hosting</returns>
        private static string GetWebHostingBindUrl(Config config)
        {
            return config.GetString("KlusterKite.Web.BindAddress", "http://*:80");
        }
    }
}