// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Startup.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The OWIN startup class.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Akka.Actor;
    using Akka.Event;
    using Akka.Configuration;

    using Autofac;
    using Autofac.Extensions.DependencyInjection;

    using JetBrains.Annotations;

    using KlusterKite.Core;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// The web hosting startup class.
    /// </summary>
    [UsedImplicitly]
    public class Startup
    {
        /// <summary>
        /// Gets source for service configuration waiter
        /// </summary>
        private static TaskCompletionSource<object> serviceConfigurationWaiter = new TaskCompletionSource<object>();

        /// <summary>
        /// Gets source for service start waiter
        /// </summary>
        private static TaskCompletionSource<object> serviceStartWaiter = new TaskCompletionSource<object>();

        /// <summary>
        /// Gets or sets the application config
        /// </summary>
        internal static Config Config { get; set; }

        /// <summary>
        /// Gets or sets the container builder
        /// </summary>
        internal static ContainerBuilder ContainerBuilder { get; set; }

        /// <summary>
        /// Gets the task to wait for container
        /// </summary>
        internal static TaskCompletionSource<IComponentContext> ContainerWaiter { get; private set; } =
            new TaskCompletionSource<IComponentContext>();

        /// <summary>
        /// Gets the task to wait for container
        /// </summary>
        internal static Task ServiceConfigurationWaiter => serviceConfigurationWaiter.Task;

        /// <summary>
        /// Gets the task to wait for container
        /// </summary>
        internal static Task ServiceStartWaiter => serviceStartWaiter.Task;

        /// <summary>
        /// Gets the last thrown exception
        /// </summary>
        internal static Exception LastException { get; private set; }

        /// <summary>
        /// Resets current initialization status.
        /// </summary>
        public static void Reset()
        {
            serviceConfigurationWaiter = new TaskCompletionSource<object>();
            serviceStartWaiter = new TaskCompletionSource<object>();
            ContainerWaiter = new TaskCompletionSource<IComponentContext>();
            LastException = null;
        }

        /// <summary>
        /// The application configuration
        /// </summary>
        /// <param name="appBuilder">
        /// The builder
        /// </param>
        /// <param name="env">
        /// The env.
        /// </param>
        /// <param name="loggerFactory">
        /// The logger Factory.
        /// </param>
        [UsedImplicitly]
        public void Configure(IApplicationBuilder appBuilder)
        {
            try
            {
                var startupConfigurators = this.GetConfigurators(Config);
                foreach (var configurator in startupConfigurators)
                {
                    appBuilder = configurator.ConfigureApplication(appBuilder, Config);
                }

                appBuilder.UseMvc();
                serviceStartWaiter.SetResult(null);
            }
            catch (Exception exception)
            {
                LastException = exception;
                throw;
            }
        }

        /// <summary>
        /// The services configuration
        /// </summary>
        /// <param name="services">The list of services</param>
        /// <returns>Service provider</returns>
        [UsedImplicitly]
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            try
            {
                services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                

                var builder = services.AddMvcCore(options => options.EnableEndpointRouting = false);
                
                foreach (var assembly in ActorSystemUtils.GetLoadedAssemblies())
                {
                    builder.AddApplicationPart(assembly);
                }

                builder.AddNewtonsoftJson(o =>
                {
                    o.SerializerSettings.Converters.Add(new StringEnumConverter());
                    o.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                });

                // builder.AddControllersAsServices();
                // builder.AddJsonFormatters();

                var startupConfigurators = this.GetConfigurators(Config);
                foreach (var configurator in startupConfigurators)
                {
                    configurator.ConfigureServices(services, Config);
                }

                ContainerBuilder.Populate(services);
                serviceConfigurationWaiter.SetResult(null);
                var container = ContainerWaiter.Task.Result;
                var system = container.Resolve<ActorSystem>();
                system.Log.Info("{Type}: ConfigureServices done", this.GetType().Name);
                // TODO: replace IComponentContext with IContainer
                return new AutofacServiceProvider(((IContainer)container).BeginLifetimeScope());
            }
            catch (Exception exception)
            {
                LastException = exception;
                throw;
            }
        }

        /// <summary>
        /// Reads configuration file and creates configurators
        /// </summary>
        /// <param name="config">The configuration</param>
        /// <returns>The list of configurators</returns>
        private IEnumerable<IWebHostingConfigurator> GetConfigurators(Config config)
        {
            var configSection = config.GetConfig("KlusterKite.Web.Configurators");
            if (configSection == null)
            {
                yield break;
            }

            foreach (var valuePair in configSection.AsEnumerable())
            {
                if (!valuePair.Value.IsString())
                {
                    continue;
                }

                var typeName = valuePair.Value.GetString();
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    var type = Type.GetType(typeName);
                    if (type != null)
                    {
                        var configurator = Activator.CreateInstance(type) as IWebHostingConfigurator;
                        if (configurator != null)
                        {
                            yield return configurator;
                        }
                    }
                }
            }
        }
    }
}