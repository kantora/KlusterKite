// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Service main entry point
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Core.Service
{
    using System;
    using System.Collections.Generic;

    using Akka.Actor;

    using Autofac;

    using JetBrains.Annotations;

    using Serilog;

    /// <summary>
    /// Service main entry point
    /// </summary>
    [UsedImplicitly]
    public class Program
    {
        /// <summary>
        /// Gets or sets the dependency injection container
        /// </summary>
        private static ContainerBuilder Container { get; set; }

        /// <summary>
        /// Service main entry point
        /// </summary>
        /// <param name="args">
        /// Startup parameters
        /// </param>
        public static void Main(string[] args)
        {
            // preset logger
            var loggerConfig = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.ColoredConsole();
            var logger = loggerConfig.CreateLogger();
            Log.Logger = logger;

            var configurations = new List<string>();
            foreach (var arg in args)
            {
                if (arg.StartsWith("--config="))
                {
                    var configFile = arg.Substring("--config=".Length);
                    configurations.Add(configFile);
                    Console.WriteLine($@"Will use config from {configFile}");
                }
            }

            Container = new ContainerBuilder();
#if APPDOMAIN
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
                {
                    Log.Logger.Error(
                        eventArgs.ExceptionObject as Exception,
                        "{Type}: Unhandled domain exception {ExceptionMessage} from {SenderType}, terminating: {IsTerminating}\n{StackTrace}", 
                        "System",
                        (eventArgs.ExceptionObject as Exception)?.Message,
                        sender?.GetType().Name ?? "unknown",
                        eventArgs.IsTerminating,
                        (eventArgs.ExceptionObject as Exception)?.StackTrace);
                };
#endif
#if CORECLR
            // todo: Handle exception
#endif

            var container = Bootstrapper.ConfigureAndStart(Container, configurations.ToArray());
            var system = container.Resolve<ActorSystem>();
            Log.Logger.Warning("{Type}: Started", "System");
            Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    Log.Logger.Warning("{Type}: Shutdown sequence initiated", "System");
                    var cluster = Akka.Cluster.Cluster.Get(system);

                    var timeout = TimeSpan.FromSeconds(10);
                    if (cluster.IsTerminated || cluster.State.Members.Count == 0)
                    {
                        system.Terminate().Wait(timeout);
                    }
                    else
                    {
                        cluster.LeaveAsync().Wait(timeout);
                        system.Terminate().Wait(timeout);
                    }

                    Log.Logger.Warning("{Type}: Hard stopped", "System");
                    eventArgs.Cancel = true;
                };

            system.StartNameSpaceActorsFromConfiguration();
            BaseInstaller.RunPostStart(Container, container);

            var waitedTask = new System.Threading.Tasks.TaskCompletionSource<bool>();
            system.WhenTerminated.ContinueWith(task => waitedTask.SetResult(true));
            waitedTask.Task.Wait();
            Log.Logger.Warning("{Type}: Stopped", "System");
        }
    }
}