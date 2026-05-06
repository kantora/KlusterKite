// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Seeder.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Seeds the <see cref="ConfigurationContext" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.ConfigurationSource.Seeder
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Akka.Configuration;

    using JetBrains.Annotations;

    using KlusterKite.Data.EF;
    using KlusterKite.NodeManager.Client;
    using KlusterKite.NodeManager.Client.ORM;
    using KlusterKite.NodeManager.Launcher.Messages;
    using KlusterKite.NodeManager.Launcher.Utils;
    using KlusterKite.NodeManager.Launcher.Utils.Exceptions;
    using KlusterKite.NodeManager.Migrator;
    using KlusterKite.Security.Attributes;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Storage;

    /// <summary>
    /// Seeds the <see cref="ConfigurationContext"/>
    /// </summary>
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class Seeder : BaseSeeder
    {
        /// <summary>
        /// Akka configuration path to connection string
        /// </summary>
        [UsedImplicitly]
        protected const string ConfigConnectionStringPath = "KlusterKite.NodeManager.ConfigurationDatabaseConnectionString";

        /// <summary>
        /// Akka configuration path to database name
        /// </summary>
        [UsedImplicitly]
        protected const string ConfigDatabaseNamePath = "KlusterKite.NodeManager.ConfigurationDatabaseName";

        /// <summary>
        /// Akka configuration path to database provider name
        /// </summary>
        [UsedImplicitly]
        protected const string ConfigDatabaseProviderNamePath = "KlusterKite.NodeManager.ConfigurationDatabaseProviderName";

        /// <summary>
        /// The context factory
        /// </summary>
        private readonly UniversalContextFactory contextFactory;

        /// <summary>
        /// The package repository
        /// </summary>
        private readonly IPackageRepository packageRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="Seeder"/> class.
        /// </summary>
        /// <param name="config">
        /// The config.
        /// </param>
        /// <param name="contextFactory">
        /// The context factory.
        /// </param>
        /// <param name="packageRepository">
        /// The package repository.
        /// </param>
        public Seeder(Config config, UniversalContextFactory contextFactory, IPackageRepository packageRepository)
        {
            this.Config = config;
            this.contextFactory = contextFactory;
            this.packageRepository = packageRepository;
        }

        /// <summary>
        /// Gets the seeder configuration
        /// </summary>
        protected Config Config { get; }

        /// <inheritdoc />
        public override void Seed()
        {
            var supportedFrameworks = this.Config.GetStringList("KlusterKite.NodeManager.SupportedFrameworks").ToList();

            // Build configuration settings (needed for both fallback generation and DB seeding)
            var configSettings = new ConfigurationSettings
            {
                NodeTemplates = this.GetNodeTemplates().ToList(),
                MigratorTemplates = this.GetMigratorTemplates().ToList(),
                Packages = this.GetPackageDescriptions().GetAwaiter().GetResult(),
                SeedAddresses = this.GetSeeds().ToList(),
                NugetFeed = this.Config.GetString("KlusterKite.NodeManager.PackageRepository")
            };

            var resolvedConfiguration = new Configuration
            {
                State = EnConfigurationState.Active,
                Name = "Initial configuration",
                Started = DateTimeOffset.Now,
                Settings = configSettings
            };

            var resolutionErrors =
                resolvedConfiguration.SetPackagesDescriptionsForTemplates(this.packageRepository, supportedFrameworks)
                    .GetAwaiter().GetResult();

            if (resolutionErrors.Any())
            {
                // Hard blocker. If any required package (or a transitive dep)
                // is missing from the local NuGet feed at this point, every
                // downstream artifact we'd produce — Initial config in the
                // DB and the fallback JSON files — would be quietly corrupt
                // and split-brain the cluster (some node templates resolve
                // version A from cache, others version B from a later
                // re-resolve).
                //
                // Fail fast and exit non-zero. The seeder.launcher's retry
                // loop will sleep NugetCheckPeriod and try again, by which
                // time the package push may have completed.
                foreach (var errorDescription in resolutionErrors)
                {
                    Console.WriteLine(
                        $@"package resolution error in {errorDescription.Field} - {errorDescription.Message}");
                }

                throw new PackageNotFoundException(
                    $"Cannot seed: {resolutionErrors.Count} package resolution error(s) "
                    + "against the local NuGet feed. Most likely the package push has not "
                    + "finished yet on a cold start. Aborting so seeder.launcher can retry.");
            }

            // Always write fallback configs so nodes can bootstrap even on fresh volumes
            var fallbackDir = this.Config.GetString("KlusterKite.NodeManager.FallbackOutputDir", string.Empty);
            if (!string.IsNullOrWhiteSpace(fallbackDir))
            {
                this.WriteFallbackConfigs(configSettings, fallbackDir);
            }

            // Seed the database only if it does not already exist
            var connectionString = this.Config.GetString(ConfigConnectionStringPath);
            var databaseName = this.Config.GetString(ConfigDatabaseNamePath);
            var databaseProviderName = this.Config.GetString(ConfigDatabaseProviderNamePath);
            using (var context =
                this.contextFactory.CreateContext<ConfigurationContext>(
                    databaseProviderName,
                    connectionString,
                    databaseName))
            {
                if (databaseProviderName != "InMemory")
                {
                    var databaseCreator = context.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                    if (databaseCreator == null)
                    {
                        Console.WriteLine(@"Error - could not check database existence. There is no IDatabaseCreator.");
                        return;
                    }

                    if (databaseCreator.Exists())
                    {
                        Console.WriteLine(@"KlusterKite configuration database is already existing");
                        // Don't touch user-edited Configurations rows, but the
                        // Active configuration's PackagesToInstall MUST stay
                        // consistent with the current NuGet feed: otherwise a
                        // node restart can pull versions that the cluster
                        // assemblies (compiled against a newer set) refuse to
                        // load. Re-resolve and persist if anything drifted.
                        this.RefreshActiveConfigurationPackages(context, supportedFrameworks);
                        return;
                    }

                    context.Database.Migrate();
                }

                this.SetupUsers(context);
                context.Configurations.Add(resolvedConfiguration);
                context.SaveChanges();
            }

            Console.WriteLine(@"KlusterKite configuration database created");
        }

        /// <summary>
        /// Re-resolves package versions for every non-terminal configuration
        /// in an existing database (Active and Ready) and persists the result
        /// if it differs.
        /// </summary>
        /// <remarks>
        /// On a fresh seed we always resolve PackageRequirements against the
        /// current NuGet feed. On a re-seed (DB already exists) we previously
        /// short-circuited, leaving PackagesToInstall frozen at whatever
        /// versions were on NuGet the day the DB was created — and at
        /// whatever SupportedFrameworks the manager hocon listed at that
        /// time. Once we publish rebuilt cluster assemblies that reference
        /// newer transitive deps or change the framework list, nodes that
        /// download per a stale config end up with a version mismatch and
        /// crash on startup with FileNotFoundException, or the MigrationActor
        /// rejects the configuration with "Framework X is not supported"
        /// because the relevant framework key is absent from PackagesToInstall.
        ///
        /// Refreshing Active is mandatory; refreshing Ready avoids a
        /// follow-on hang when the user picks up a Ready candidate that was
        /// validated against the old framework list. Drafts stay untouched
        /// (they're user-editable and re-resolve via the UI's Check button);
        /// Obsolete/Archived/Faulted configurations stay frozen.
        /// </remarks>
        /// <param name="context">An open ConfigurationContext on the existing DB</param>
        /// <param name="supportedFrameworks">Frameworks for which to resolve PackagesToInstall</param>
        private void RefreshActiveConfigurationPackages(ConfigurationContext context, List<string> supportedFrameworks)
        {
            var configurations = context.Configurations
                .Where(c => c.State == EnConfigurationState.Active
                            || c.State == EnConfigurationState.Ready)
                .ToList();
            if (!configurations.Any())
            {
                Console.WriteLine("No Active/Ready configurations found - nothing to refresh.");
                return;
            }

            foreach (var configuration in configurations)
            {
                // Re-resolve the flat package list from NuGet so Packages reflects
                // current nuget contents (latest available version per id), then
                // recompute PackagesToInstall transitive closures.
                configuration.Settings.Packages = this.GetPackageDescriptions().GetAwaiter().GetResult();

                var refreshErrors = configuration
                    .SetPackagesDescriptionsForTemplates(this.packageRepository, supportedFrameworks)
                    .GetAwaiter().GetResult();

                if (refreshErrors.Any())
                {
                    foreach (var err in refreshErrors)
                    {
                        Console.WriteLine(
                            $@"Configuration #{configuration.Id} ('{configuration.Name}', {configuration.State}) refresh: package resolution error in {err.Field} - {err.Message}");
                    }

                    throw new PackageNotFoundException(
                        $"Cannot refresh configuration #{configuration.Id} ('{configuration.Name}'): "
                        + $"{refreshErrors.Count} package resolution error(s). Aborting so "
                        + "seeder.launcher can retry once the NuGet feed is fully populated.");
                }

                // Settings is serialized into the SettingsJson column. EF
                // tracks the string, not the in-memory object, so we have to
                // tell it the column is dirty after we mutate Settings.
                context.Entry(configuration).Property(c => c.SettingsJson).IsModified = true;
                Console.WriteLine(
                    $"Refreshed configuration #{configuration.Id} ('{configuration.Name}', {configuration.State}) against current NuGet feed.");
            }

            context.SaveChanges();
        }

        /// <summary>
        /// Writes NodeStartUpConfiguration fallback JSON files for each node template so launchers
        /// can bootstrap without calling the NodeManager API on first cluster start.
        /// </summary>
        /// <param name="configSettings">Resolved configuration settings (PackagesToInstall must be populated)</param>
        /// <param name="outputDir">Directory path to write the JSON files into</param>
        private void WriteFallbackConfigs(ConfigurationSettings configSettings, string outputDir)
        {
            Directory.CreateDirectory(outputDir);

            var frameworkKey = PackageRepositoryExtensions.Net9;

            foreach (var template in configSettings.NodeTemplates)
            {
                if (template.PackagesToInstall == null
                    || !template.PackagesToInstall.TryGetValue(frameworkKey, out var packages)
                    || packages == null
                    || packages.Count == 0)
                {
                    Console.WriteLine($"Skipping fallback for template '{template.Code}': packages not resolved for {frameworkKey}");
                    continue;
                }

                var fallback = new NodeStartUpConfiguration
                {
                    Configuration = template.Configuration,
                    NodeTemplate = template.Code,
                    ConfigurationId = 1,
                    Packages = packages,
                    PackageSource = configSettings.NugetFeed,
                    Seeds = configSettings.SeedAddresses
                };

                var filePath = Path.Combine(outputDir, $"{template.Code}.json");
                var json = JsonSerializer.Serialize(fallback);
                File.WriteAllText(filePath, json);
                Console.WriteLine($"Wrote fallback config for template '{template.Code}' ({packages.Count} packages) to {filePath}");
            }
        }

        /// <summary>
        /// Gets the list of akka cluster seeds
        /// </summary>
        /// <returns>The list of seed addresses</returns>
        [UsedImplicitly]
        protected virtual IEnumerable<string> GetSeeds()
        {
            return this.Config.GetStringList("KlusterKite.NodeManager.Seeds");
        }

        /// <summary>
        /// Get the list of node templates
        /// </summary>
        /// <returns>The list of node templates</returns>
        [UsedImplicitly]
        protected virtual IEnumerable<NodeTemplate> GetNodeTemplates()
        {
            yield return new NodeTemplate
                             {
                                 Code = "publisher",
                                 Name = "Cluster Nginx configurator",
                                 MinimumRequiredInstances = 1,
                                 MaximumNeededInstances = null,
                                 ContainerTypes = new List<string> { "publisher" },
                                 Priority = 1000.0,
                                 PackageRequirements =
                                     new[]
                                         {
                                             "KlusterKite.Core.Service",
                                             "KlusterKite.Web.NginxConfigurator",
                                             "KlusterKite.NodeManager.Client",
                                             "KlusterKite.Log.Console",
                                             "KlusterKite.Log.ElasticSearch",
                                             "KlusterKite.Monitoring.Client",
                                         }.Select(p => new NodeTemplate.PackageRequirement(p, null)).ToList(),
                                 Configuration = ConfigurationUtils.ReadTextResource(this.GetType().GetTypeInfo().Assembly, "KlusterKite.NodeManager.ConfigurationSource.Seeder.Resources.publisher.hocon")
                             };
            yield return new NodeTemplate
                             {
                                 Code = "clusterManager",
                                 Name = "Cluster manager (cluster monitoring and managing)",
                                 MinimumRequiredInstances = 1,
                                 MaximumNeededInstances = 3,
                                 ContainerTypes = new List<string> { "manager", "worker" },
                                 Priority = 100.0,
                                 PackageRequirements =
                                     new[]
                                         {
                                             "KlusterKite.Core.Service",
                                             "KlusterKite.NodeManager.Client",
                                             "KlusterKite.Monitoring.Client",
                                             "KlusterKite.Monitoring",
                                             "KlusterKite.NodeManager",
                                             "KlusterKite.Data.EF.Npgsql",
                                             "KlusterKite.Log.Console",
                                             "KlusterKite.Log.ElasticSearch",
                                             "KlusterKite.Web.Authentication",
                                             "KlusterKite.NodeManager.Authentication",
                                             "KlusterKite.Security.SessionRedis",
                                             "KlusterKite.API.Endpoint",
                                             "KlusterKite.Web.GraphQL.Publisher"
                                         }.Select(p => new NodeTemplate.PackageRequirement(p, null)).ToList(),
                                 Configuration = ConfigurationUtils.ReadTextResource(this.GetType().GetTypeInfo().Assembly, "KlusterKite.NodeManager.ConfigurationSource.Seeder.Resources.clusterManager.hocon")
            };

            yield return new NodeTemplate
                             {
                                 Code = "empty",
                                 Name = "Cluster empty instance, just for demo",
                                 MinimumRequiredInstances = 0,
                                 MaximumNeededInstances = null,
                                 ContainerTypes = new List<string> { "worker" },
                                 Priority = 1.0,
                                 PackageRequirements =
                                     new[]
                                         {
                                             "KlusterKite.Core.Service",
                                             "KlusterKite.NodeManager.Client",
                                             "KlusterKite.Monitoring.Client"
                                         }.Select(p => new NodeTemplate.PackageRequirement(p, null)).ToList(),
                                 Configuration = ConfigurationUtils.ReadTextResource(this.GetType().GetTypeInfo().Assembly, "KlusterKite.NodeManager.ConfigurationSource.Seeder.Resources.empty.hocon")
            };
        }

        /// <summary>
        /// Get the list of migrator templates
        /// </summary>
        /// <returns>The list of migrator templates</returns>
        protected virtual IEnumerable<MigratorTemplate> GetMigratorTemplates()
        {
            yield return new MigratorTemplate
                             {
                                 Name = "KlusterKite Migrator",
                                 Code = "KlusterKite",
                                 Configuration = ConfigurationUtils.ReadTextResource(this.GetType().GetTypeInfo().Assembly, "KlusterKite.NodeManager.ConfigurationSource.Seeder.Resources.migrator.hocon"),
                                 PackageRequirements =
                                     new[]
                                         {
                                            new NodeTemplate.PackageRequirement(
                                                 "KlusterKite.NodeManager",
                                                 null),
                                             new NodeTemplate.PackageRequirement(
                                                 "KlusterKite.NodeManager.Mock",
                                                 null),
                                             new NodeTemplate.PackageRequirement(
                                                 "KlusterKite.Data.EF.Npgsql",
                                                 null),
                                         }.ToList(),
                                 Priority = 1d
                             };
        }

        /// <summary>
        /// Get the list of package descriptions
        /// </summary>
        /// <returns>The list of package descriptions</returns>
        protected virtual async Task<List<PackageDescription>> GetPackageDescriptions()
        {
            return (await this.packageRepository.SearchAsync(string.Empty, true))
                .Select(p => new PackageDescription(p.Identity.Id, p.Identity.Version.ToString())).ToList();
        }

        /// <summary>
        /// Installs default users and roles to the empty database
        /// </summary>
        /// <param name="context">The data context</param>
        [UsedImplicitly]
        protected virtual void SetupUsers(ConfigurationContext context)
        {
            if (context.Users.Any() || context.Roles.Any())
            {
                return;
            }

            var adminPrivileges =
                new List<IEnumerable<PrivilegeDescription>>
                    {
                        Utils.GetDefinedPrivileges(typeof(Privileges)),

                        Utils.GetDefinedPrivileges(
                            typeof(Monitoring.Client.Privileges))
                    };

            var adminRole = new Role
                                {
                                    Uid = Guid.NewGuid(),
                                    Name = "Admin",
                                    AllowedScope = adminPrivileges.SelectMany(l => l.Select(p => p.Privilege))
                                        .ToList()
                                };
            var guestRole = new Role
                                {
                                    Uid = Guid.NewGuid(),
                                    Name = "Guest",
                                    AllowedScope =
                                        new List<string>
                                            {
                                                Privileges.GetActiveNodeDescriptions,
                                                Privileges.GetTemplateStatistics,
                                                $"{Privileges.Configuration}.Query"
                                            }
                                };
            context.Roles.Add(adminRole);
            context.Roles.Add(guestRole);

            var adminUser = new User
                                {
                                    Uid = Guid.NewGuid(),
                                    Login = "admin",
                                    Roles = new List<RoleUser> { new RoleUser { Role = adminRole } }
                                };
            adminUser.SetPassword("admin");
            var guestUser = new User
                                {
                                    Uid = Guid.NewGuid(),
                                    Login = "guest",
                                    Roles = new List<RoleUser> { new RoleUser { Role = guestRole } }
                                };
            guestUser.SetPassword("guest");

            context.Users.Add(adminUser);
            context.Users.Add(guestUser);
        }
    }
}
