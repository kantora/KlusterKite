// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Bootstrapper.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Dependency injection configuration
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Core.Service
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Akka.Actor;
    using Akka.Configuration;
    using Akka.DI.AutoFac;
    using Akka.DI.Core;

    using Autofac;

    using JetBrains.Annotations;

    using KlusterKite.Core.Log;

    using Serilog;
    using Serilog.Events;

    /// <summary>
    /// Dependency injection configuration
    /// </summary>
    [UsedImplicitly]
    public class Bootstrapper
    {
        /// <summary>
        ///  Dependency injection configuration
        /// </summary>
        /// <param name="containerBuilder">Dependency injection container</param>
        /// <param name="configurations">
        /// Startup parameters
        /// </param>
        /// <returns>The DI container</returns>
        public static IContainer ConfigureAndStart(ContainerBuilder containerBuilder, string[] configurations)
        {
            Console.WriteLine(@"Starting bootstrapper");

            containerBuilder.RegisterInstallers();

            Console.WriteLine(@"Preparing config");
            var config = BaseInstaller.GetStackedConfig(containerBuilder, CreateTopLevelConfig(configurations));

            Log.Debug($"Cluster configuration: seed-nodes { string.Join(", ", config.GetStringList("akka.cluster.seed-nodes") ?? new List<string>())}");
            Log.Debug($"Cluster configuration: min-nr-of-members { config.GetInt("akka.cluster.min-nr-of-members")}");
            var roles = string.Join(", ", config.GetStringList("akka.cluster.roles") ?? new List<string>());
            Log.Debug($"Cluster configuration: roles { roles}");
            Log.Debug($"Cluster node hostname: { config.GetString("akka.remote.helios.tcp.hostname") ?? config.GetString("akka.remote.dot-netty.tcp.hostname") }");
            var publicHostName = config.GetString("akka.remote.helios.tcp.public-hostname")
                ?? config.GetString("akka.remote.dot-netty.tcp.public-hostname");
            if (!string.IsNullOrWhiteSpace(publicHostName))
            {
                Log.Debug($"Cluster node public hostname: { publicHostName }");
            }

            containerBuilder.RegisterInstance(config).As<Config>();
            BaseInstaller.RunComponentRegistration(containerBuilder, config);

            Console.WriteLine(@"Config created");

            // log configuration
            LogEventLevel level;
            if (!Enum.TryParse(config.GetString("KlusterKite.Log.minimumLevel"), true, out level))
            {
                level = LogEventLevel.Verbose;
            }

            var actorSystem = ActorSystem.Create("KlusterKite", config);
            containerBuilder.RegisterInstance(actorSystem).As<ActorSystem>();
            var container = containerBuilder.Build();

            var loggerConfig = new LoggerConfiguration().MinimumLevel.Is(level);
            var configurators = container.Resolve<IEnumerable<ILoggerConfigurator>>().ToList();

            configurators.ForEach(c => Log.Information("Using log configurator {TypeName}", c.GetType().FullName));

            loggerConfig = configurators.Aggregate(
                loggerConfig,
                (current, loggerConfigurator) => loggerConfigurator.Configure(current, config));

            var hostName = string.IsNullOrWhiteSpace(publicHostName)
                               ? config.GetString("akka.remote.helios.tcp.hostname")
                               : publicHostName;

            loggerConfig = loggerConfig.Enrich.WithProperty("hostName", hostName);
            loggerConfig = loggerConfig.Enrich.WithProperty("roles", roles);

            var logger = loggerConfig.CreateLogger();
            Log.Logger = logger;

            // log configuration finished

            // performing pre-start checks
            BaseInstaller.RunPreCheck(containerBuilder, config);

            // starting Akka system
            Console.WriteLine(@"starting akka system");
            actorSystem.AddDependencyResolver(new AutoFacDependencyResolver(container, actorSystem));

            Console.WriteLine(@"Bootstrapper start finished");

            return container;
        }

        /// <summary>
        /// Creates configuration file from environment variables, start-up parameters and local files
        /// </summary>
        /// <param name="configurations">Start up parameters</param>
        /// <returns>Top level configuration</returns>
        /// <remarks>
        /// Load priority:
        ///     * List of files in start up parameters in order of parameters
        ///     * environment variables
        ///     * akka.hocon file in local application directory
        ///     * Akka config section in application configuration file
        /// </remarks>
        private static Config CreateTopLevelConfig(string[] configurations)
        {
            var config = ConfigurationFactory.Empty;
            if (configurations != null)
            {
                Console.WriteLine($@"loading config from command line parameters: {string.Join(", ", configurations)}");
                config = configurations
                    .Where(File.Exists)
                    .Select(File.ReadAllText)
                    .Aggregate(
                    config,
                        (current, configText) => current.WithFallback(ConfigurationFactory.ParseString(configText)));
            }

            Console.WriteLine(@"loading config from environment");
            var networkName = Environment.GetEnvironmentVariable("NETWORK_NAME");
            if (string.IsNullOrEmpty(networkName))
            {
                try
                {
                    var hostname = System.Net.Dns.GetHostName();
                    var hostEntry = System.Net.Dns.GetHostEntry(hostname);
                    var ip = hostEntry.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    networkName = ip?.ToString() ?? hostname;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($@"Failed to resolve hostname: {ex.Message}");
                }
                Console.WriteLine($@"NETWORK_NAME not set, resolved to: {networkName}");
            }
            if (!string.IsNullOrEmpty(networkName))
            {
                Console.WriteLine($@"Setting public hostname to: {networkName}");
                var escapedName = networkName.Replace("\"", "\\\"");
                config = config.WithFallback(ConfigurationFactory.ParseString($"{{ akka.remote.helios.tcp.public-hostname = \"{escapedName}\" }}"));
                config = config.WithFallback(ConfigurationFactory.ParseString($"{{ akka.remote.dot-netty.tcp.public-hostname = \"{escapedName}\" }}"));
            }

            // ReSharper disable once AssignNullToNotNullAttribute
            var hoconPath = Path.Combine(Path.GetDirectoryName(typeof(Bootstrapper).GetTypeInfo().Assembly.Location), "akka.hocon");
            Console.WriteLine($@"loading akka.hocon ({hoconPath})");
            if (File.Exists(hoconPath))
            {
                Console.WriteLine($@"reading file ({hoconPath})");
                var hoconConfig = File.ReadAllText(hoconPath);
                Console.WriteLine(@"file loaded in memory");
                config = config.WithFallback(ConfigurationFactory.ParseString(hoconConfig));
            }
            else
            {
                Log.Warning("File {fileName} was not found", hoconPath);
            }

            Console.WriteLine(@"loading application configuration");

            return config;
        }
    }
}