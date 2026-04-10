// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Service main entry point
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Seeder.Launcher
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Akka.Configuration;

    using JetBrains.Annotations;

    using KlusterKite.NodeManager.Launcher.Utils;
    using KlusterKite.NodeManager.Launcher.Utils.Exceptions;

    using NuGet.Frameworks;
    using NuGet.Packaging.Core;
    using NuGet.Protocol.Core.Types;

    /// <summary>
    /// Service main entry point
    /// </summary>
    [UsedImplicitly]
    public class Program
    {
        /// <summary>
        /// Service main entry point
        /// </summary>
        /// <param name="args">
        /// Startup parameters
        /// </param>
        public static void Main(string[] args)
        {
            var configuration = ReadConfiguration(args);
            if (configuration == null)
            {
                return;
            }

            foreach (var seederConfiguration in configuration.Configurations)
            {
                RunSeederConfiguration(configuration, seederConfiguration);
            }
        }

        /// <summary>
        /// Runs seeder configuration
        /// </summary>
        /// <param name="configuration">The global configuration</param>
        /// <param name="seederConfiguration">The seeder configuration</param>
        private static void RunSeederConfiguration(Configuration configuration, SeederConfiguration seederConfiguration)
        {
            var executionDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(executionDir);

            try
            {
                while (true)
                {
                    try
                    {
                        var repository = new RemotePackageRepository(configuration.Nuget);
                        var packages = GetPackagesToInstall(repository, seederConfiguration).GetAwaiter()
                            .GetResult();

                        repository.CreateServiceAsync(
                            packages,
                            configuration.Runtime,
                            PackageRepositoryExtensions.CurrentRuntime,
                            executionDir,
                            "KlusterKite.NodeManager.Seeder",
                            Console.WriteLine).GetAwaiter().GetResult();
                            
                        Console.WriteLine("Packages installed");
                    }
                    catch (PackageNotFoundException packageNotFoundException)
                    {
                        Console.WriteLine($"Package {packageNotFoundException.Message} was not found");
                        Thread.Sleep(configuration.NugetCheckPeriod);
                        continue;
                    }
                    catch (AggregateException exception)
                    {
                        var e = exception.InnerExceptions?.FirstOrDefault() ?? exception;
                        Console.WriteLine(e.Message);
                        Console.WriteLine(e.StackTrace);
                        Thread.Sleep(configuration.NugetCheckPeriod);
                        continue;
                    }
                    catch (Exception exception)
                   {
                       Console.WriteLine(exception.Message);
                       Console.WriteLine(exception.StackTrace);
                        Thread.Sleep(configuration.NugetCheckPeriod);
                       continue;
                   }

                    try
                    {
                        File.Copy(configuration.ConfigFile, Path.Combine(executionDir, "seeder.hocon"), true);
                        
                        // todo: move to CreateServiceAsync
                        // ConfigurationUtils.FixAssemblyVersions(Path.Combine(executionDir, "KlusterKite.NodeManager.Seeder.exe.config"));
                        Console.WriteLine($"Seeder prepared in {executionDir}");
#if APPDOMAIN
                        var process = new Process
                                          {
                                              StartInfo =
                                                  {
                                                      UseShellExecute = false,
                                                      WorkingDirectory =executionDir,
                                                      FileName = "KlusterKite.NodeManager.Seeder.exe",
                                                      Arguments = seederConfiguration.Name
                                                  }
                                          };
#elif CORECLR
                        var process = new Process
                                          {
                                              StartInfo =
                                                  {
                                                      UseShellExecute = false,
                                                      WorkingDirectory = executionDir,
                                                      FileName = "dotnet",
                                                      Arguments = $"KlusterKite.NodeManager.Seeder.dll {seederConfiguration.Name}"
                                                  }
                                          };
#endif
                        process.Start();
                        process.WaitForExit();
                        process.Dispose();
                        Console.WriteLine("Seeder stopped");
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(e.StackTrace);
                        Console.ReadLine();
                        break;
                    }
                }
            }
            finally
            {
                try
                {
                    Directory.Delete(executionDir, true);
                }
                catch
                {
                    Console.WriteLine("Failed to remove execution directory");
                }
            }
        }

        /// <summary>
        /// Gets the list of packages to install along with there dependencies
        /// </summary>
        /// <param name="repository">The package repository</param>
        /// <param name="seederConfiguration">The seeder configuration</param>
        /// <returns>The list of packages</returns>
        private static async Task<IEnumerable<PackageIdentity>> GetPackagesToInstall(
            IPackageRepository repository,
            SeederConfiguration seederConfiguration)
        {
            // TODO: extract to helper method along with Configuration extensions
            var supportedFramework = NuGetFramework.ParseFrameworkName(
                PackageRepositoryExtensions.CurrentRuntime, 
                DefaultFrameworkNameProvider.Instance);

            var packageTasks =
                seederConfiguration.RequiredPackages.Select(
                    async packageName =>
                        {
                            var package = await repository.GetAsync(packageName);
                            if (package == null)
                            {
                                throw new PackageNotFoundException(packageName);
                            }

                            return package;
                        });

            var packagesToInstall = (await Task.WhenAll(packageTasks)).ToDictionary(p => p.Identity.Id);
            var queue = new Queue<IPackageSearchMetadata>(packagesToInstall.Values);

            while (queue.Count > 0)
            {
                var package = queue.Dequeue();
                var dependencySet =
                    NuGetFrameworkUtility.GetNearest(package.DependencySets, supportedFramework);
                if (dependencySet == null || !NuGetFrameworkUtility.IsCompatibleWithFallbackCheck(
                        supportedFramework,
                        dependencySet.TargetFramework))
                {
                    continue;
                }

                foreach (var dependency in dependencySet.Packages)
                {
                    IPackageSearchMetadata packageToInstall;
                    if (!packagesToInstall.TryGetValue(dependency.Id, out packageToInstall))
                    {
                        packageToInstall = await repository.GetAsync(dependency.Id);
                        if (packageToInstall == null)
                        {
                            // Framework packages (System.*, Microsoft.NETCore.*) are provided by the
                            // .NET runtime and won't be on the local NuGet server — skip them.
                            Console.WriteLine($"Skipping framework/unavailable dependency: {dependency.Id}");
                            continue;
                        }

                        packagesToInstall.Add(dependency.Id, packageToInstall);
                        if (queue.All(p => p.Identity.Id != packageToInstall.Identity.Id))
                        {
                            queue.Enqueue(packageToInstall);
                        }
                    }
                }
            }

            return packagesToInstall.Values.Select(p => p.Identity);
        }

        /// <summary>
        /// Reads the configuration
        /// </summary>
        /// <param name="args">The program command line arguments</param>
        /// <returns>The configuration</returns>
        private static Configuration ReadConfiguration(string[] args)
        {
            var configuration = new Configuration();
            configuration.ConfigFile = args != null && args.Length > 0 ? args[0] : "seeder.hocon";
            if (configuration.ConfigFile == null || !File.Exists(configuration.ConfigFile))
            {
                Console.WriteLine("Configuration file was not found");
                return null;
            }

            configuration.Config = ConfigurationFactory.ParseString(File.ReadAllText(configuration.ConfigFile));
            configuration.Nuget = configuration.Config.GetString("KlusterKite.NodeManager.PackageRepository");
            if (string.IsNullOrWhiteSpace(configuration.Nuget))
            {
                Console.WriteLine("Nuget is not properly defined");
                return null;
            }

            configuration.NugetCheckPeriod =
                configuration.Config.GetTimeSpan("NugetCheckPeriod", TimeSpan.FromMinutes(1));
            configuration.Runtime = configuration.Config.GetString("Runtime");

            configuration.Configurations = new List<SeederConfiguration>();
            foreach (var subConfig in configuration.Config.GetStringList("SeederConfigurations"))
            {
                var seederConfig = configuration.Config.GetConfig(subConfig);
                if (seederConfig == null)
                {
                    Console.WriteLine($"SeederConfiguration {subConfig} was not found in the config");
                    return null;
                }

                var seederConfiguration = new SeederConfiguration(subConfig, seederConfig);
                if (seederConfiguration.RequiredPackages == null || seederConfiguration.RequiredPackages.Count == 0)
                {
                    Console.WriteLine($"SeederConfiguration {subConfig} misses the RequiredPackages property");
                    return null;
                }

                if (seederConfiguration.Seeders == null || seederConfiguration.Seeders.Count == 0)
                {
                    Console.WriteLine($"SeederConfiguration {subConfig} misses the Seeders property");
                    return null;
                }

                configuration.Configurations.Add(seederConfiguration);
            }

            if (configuration.Configurations.Count == 0)
            {
                Console.WriteLine("SeederConfiguration is empty");
                return null;
            }

            return configuration;
        }
    }
}