// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConfigurationCheckTestsBase.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Prepares the test environment to test  <see cref="ConfigurationExtensions" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using KlusterKite.API.Client;
    using KlusterKite.NodeManager.Client.ORM;
    using KlusterKite.NodeManager.ConfigurationSource;
    using KlusterKite.NodeManager.Launcher.Messages;
    using KlusterKite.NodeManager.Tests.Mock;

    using NuGet.Frameworks;
    using NuGet.Packaging;
    using NuGet.Packaging.Core;
    using NuGet.Versioning;

    using Xunit.Abstractions;

    /// <summary>
    /// Prepares the test environment to test  <see cref="ConfigurationExtensions"/>
    /// </summary>
    public abstract class ConfigurationCheckTestsBase
    {
        /// <summary>
        /// The .NET Framework 4.6 name
        /// </summary> 
        public const string Net46 = ".NETFramework,Version=v4.6";

        /// <summary>
        /// The .NET Core 1.1 name
        /// </summary>
        public const string NetCore = ".NETCoreApp,Version=v1.1";

        /// <summary>
        /// The .NET 9.0 name
        /// </summary>
        public const string Net9 = ".NETCoreApp,Version=v9.0";

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationCheckTestsBase"/> class.
        /// </summary>
        /// <param name="output">
        /// The output.
        /// </param>
        protected ConfigurationCheckTestsBase(ITestOutputHelper output)
        {
            this.Output = output;
        }

        /// <summary>
        /// Gets the test output stream
        /// </summary>
        protected ITestOutputHelper Output { get; }

        /// <summary>
        /// Creates a <see cref="PackageDependencyGroup"/> from string definition
        /// </summary>
        /// <param name="framework">The framework name</param>
        /// <param name="definition">The dependencies definition</param>
        /// <returns>The dependency set</returns>
        internal static PackageDependencyGroup CreatePackageDependencySet(string framework, params string[] definition)
        {
            return new PackageDependencyGroup(
                NuGetFramework.ParseFrameworkName(framework, DefaultFrameworkNameProvider.Instance),
                definition.Select(
                    d =>
                        {
                            var parts = d.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                            return new PackageDependency(parts[0], CreateVersionRange(parts[1]));
                        }));
        }

        /// <summary>
        /// Creates the list of package descriptions.
        /// </summary>
        /// <param name="packages">
        /// The string descriptions.
        /// </param>
        /// <returns>
        /// The list of package descriptions.
        /// </returns>
        internal static IEnumerable<PackageDescription> CreatePackageDescriptions(params string[] packages)
        {
            return packages.Select(
                p =>
                    {
                        var parts = p.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        return new PackageDescription(parts[0], parts[1]);
                    });
        }

        /// <summary>
        /// Creates the list of package requirements.
        /// </summary>
        /// <param name="packages">
        /// The string descriptions.
        /// </param>
        /// <returns>
        /// The list of package requirements.
        /// </returns>
        internal static List<NodeTemplate.PackageRequirement> CreatePackageRequirement(params string[] packages)
        {
            return packages.Select(
                p =>
                    {
                        var parts = p.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        return new NodeTemplate.PackageRequirement(parts[0], parts.Length > 1 ? parts[1] : null);
                    }).ToList();
        }

        /// <summary>
        /// Creates the default configuration
        /// </summary>
        /// <param name="packages">The list of defined packages to override</param>
        /// <param name="templatePackageRequirements">The template package requirements</param>
        /// <returns>The configuration</returns>
        internal static Configuration CreateConfiguration(string[] packages = null, string[] templatePackageRequirements = null)
        {
            if (packages == null)
            {
                packages = new[] { "p1 1.0.0", "p2 1.0.0", "dp1 1.0.0", "dp2 1.0.0", "KlusterKite.NodeManager.Migrator.Executor 1.0.0" };
            }

            if (templatePackageRequirements == null)
            {
                templatePackageRequirements = new[] { "p1", "p2 1.0.0" };
            }

            var packageDescriptions = new List<PackageDescription>(CreatePackageDescriptions(packages));

            var nodeTemplates = new List<NodeTemplate>();
            var t1 = new NodeTemplate
                         {
                             Code = "t1",
                             Configuration = "t1",
                             PackageRequirements = CreatePackageRequirement(templatePackageRequirements),
                             ContainerTypes = new List<string> { "test" }
                         };
            nodeTemplates.Add(t1);

            var migratorTemplates = new List<MigratorTemplate>();
            var m1 = new MigratorTemplate
                         {
                             Code = "m1",
                             Configuration = "m1",
                             PackageRequirements =
                                 CreatePackageRequirement(templatePackageRequirements)
                         };
            migratorTemplates.Add(m1);

            var configurationSettings =
                new ConfigurationSettings
                    {
                        Packages = packageDescriptions,
                        NodeTemplates = nodeTemplates,
                        MigratorTemplates = migratorTemplates
                    };

            return new Configuration { Settings = configurationSettings };
        }

        /// <summary>
        /// Creates a test repository
        /// </summary>
        /// <returns>The test repository</returns>
        internal static TestRepository CreateRepository()
        {
            var p1 = new TestPackage("p1", "1.0.0")
                         {
                             DependencySets =
                                 new[]
                                     {
                                         CreatePackageDependencySet(Net46, "dp1 1.0.0"),
                                         CreatePackageDependencySet(
                                             NetCore,
                                             "dp1 1.0.0")
                                     }
                         };

            var p2 = new TestPackage("p2", "1.0.0")
                         {
                             DependencySets =
                                 new[]
                                     {
                                         CreatePackageDependencySet(Net46, "dp2 1.0.0"),
                                         CreatePackageDependencySet(
                                             NetCore,
                                             "dp2 1.0.0")
                                     }
                         };

            var p3 = new TestPackage("p3", "1.0.0")
                         {
                             DependencySets =
                                 new[]
                                     {
                                         CreatePackageDependencySet(Net46, "dp3 2.0.0"),
                                         CreatePackageDependencySet(
                                             NetCore,
                                             "dp3 2.0.0")
                                     }
                         };
            var dp1 = new TestPackage("dp1", "1.0.0");

            var dp2 = new TestPackage("dp2", "1.0.0");

            var dp3 = new TestPackage("dp3", "1.0.0");

            var executor = new TestPackage("KlusterKite.NodeManager.Migrator.Executor", "1.0.0");

            return new TestRepository(p1, p2, p3, dp1, dp2, dp3, executor);
        }

        /// <summary>
        /// Creates the version range
        /// </summary>
        /// <param name="minVersion">The minimum required version</param>
        /// <returns>The version range</returns>
        internal static VersionRange CreateVersionRange(string minVersion)
        {
            return new VersionRange(NuGetVersion.Parse(minVersion));
        }

        /// <summary>
        /// Writes the error list to the output
        /// </summary>
        /// <param name="errors">The output list</param>
        internal void WriteErrors(IEnumerable<ErrorDescription> errors)
        {
            foreach (var error in errors)
            {
                this.Output.WriteLine($"{error.Field}: {error.Message}");
            }
        }
    }
}