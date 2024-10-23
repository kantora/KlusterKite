// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestRepository.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The test nuget repository
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Tests.Mock
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using KlusterKite.NodeManager.Launcher.Utils;
#if CORECLR
    using Microsoft.Extensions.DependencyModel;
#endif

    using NuGet.Frameworks;
    using NuGet.Packaging;
    using NuGet.Packaging.Core;
    using NuGet.Protocol.Core.Types;
    using NuGet.Versioning;

    /// <summary>
    /// The test nuget repository
    /// </summary>
    internal class TestRepository : IPackageRepository
    {
        /// <inheritdoc />
        public TestRepository(params TestPackage[] packages)
        {
            this.Packages = packages?.ToList() ?? new List<TestPackage>();
        }

        /// <summary>
        /// Gets the list of defined packages packages.
        /// </summary>
        [UsedImplicitly]
        public List<TestPackage> Packages { get; }

        /// <summary>
        ///     Creates the test repository
        /// </summary>
        /// <returns>The test repository</returns>
        public static IPackageRepository CreateRepositoryFromLoadedAssemblies()
        {
            var loadedAssemblies = GetLoadedAssemblies()
                .Where(a => !a.IsDynamic)
                .ToList();

            var ignoredAssemblies = loadedAssemblies.SelectMany(a => a.GetReferencedAssemblies())
                .Where(r => loadedAssemblies.All(a => a.FullName != r.FullName)).Select(r => r.FullName).ToList();

            var assemblies = loadedAssemblies.ToArray();
            var packages = assemblies.Where(a => !ignoredAssemblies.Contains(a.FullName))
                .Select(p => CreateTestPackage(p, assemblies)).GroupBy(a => a.Identity.Id)
                .Select(g => g.OrderByDescending(a => a.Identity.Id).First()).ToArray();
            return new TestRepository(packages);
        }

        /// <inheritdoc />
        public Task<IEnumerable<string>> ExtractPackage(
            IPackageSearchMetadata package,
            string frameworkName,
            string executionDir,
            string tmpDir)
        {
            var testPackage = package as TestPackage;
            if (testPackage == null)
            {
                throw new InvalidOperationException();
            }

            return Task.FromResult(testPackage.Extract(frameworkName, executionDir, tmpDir));
        }

        /// <inheritdoc />
        public async Task<Dictionary<PackageIdentity, IEnumerable<string>>> ExtractPackage(
            IEnumerable<PackageIdentity> packages,
            string runtime,
            string frameworkName,
            string executionDir,
            string tmpDir,
            Action<string> logAction = null)
        {
            var packagesToExtract = packages.Select(p => this.Packages.First(m => m.Identity.Equals(p)));
            var tasks = packagesToExtract.ToDictionary(
                p => p.Identity,
                async p => await this.ExtractPackage(p, frameworkName, executionDir, tmpDir));
            await Task.WhenAll(tasks.Values);

            return tasks.ToDictionary(p => p.Key, p => p.Value.Result);
        }

        /// <inheritdoc />
        public Task<IPackageSearchMetadata> GetAsync(string id)
        {
            return Task.FromResult<IPackageSearchMetadata>(
                this.Packages.Where(p => p.Identity.Id == id).OrderByDescending(p => p.Identity.Version)
                    .FirstOrDefault());
        }

        /// <inheritdoc />
        public Task<IPackageSearchMetadata> GetAsync(string id, NuGetVersion version)
        {
            return Task.FromResult<IPackageSearchMetadata>(
                this.Packages.FirstOrDefault(p => p.Identity.Id == id && p.Identity.Version == version));
        }

        /// <inheritdoc />
        public Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string terms, bool includePreRelease)
        {
            return Task.FromResult(
                this.Packages.Where(p => p.Identity.Id.ToLowerInvariant().Contains(terms.ToLowerInvariant()))
                    .Cast<IPackageSearchMetadata>());
        }

        /// <summary>
        /// Creates test package from assembly
        /// </summary>
        /// <param name="assembly">The source assembly</param>
        /// <param name="allAssemblies">The list of all defined assemblies</param>
        /// <returns>The test package</returns>
        private static TestPackage CreateTestPackage(Assembly assembly, Assembly[] allAssemblies)
        {
            var dependencies = assembly.GetReferencedAssemblies().Select(
                d =>
                    {
                        var dependentAssembly = allAssemblies.FirstOrDefault(a => a.GetName().Name == d.Name);
                        return dependentAssembly != null && !dependentAssembly.IsDynamic
                                   ? dependentAssembly
                                   : null;
                    }).Where(d => d != null).Select(
                d => new PackageDependency(
                    d.GetName().Name,
                    new VersionRange(NuGetVersion.Parse(d.GetName().Version.ToString())))).ToList();

            var standardDependencies = new PackageDependencyGroup(
                NuGetFramework.ParseFrameworkName(
                    ConfigurationCheckTestsBase.NetCore,
                    DefaultFrameworkNameProvider.Instance),
                dependencies);
            var net46Dependencies = new PackageDependencyGroup(
                NuGetFramework.ParseFrameworkName(
                    ConfigurationCheckTestsBase.Net46,
                    DefaultFrameworkNameProvider.Instance),
                dependencies);

            Func<string, string, string, IEnumerable<string>> extaction = (framework, destination, temp) =>
                {
                    if (string.IsNullOrWhiteSpace(assembly.Location))
                    {
                        throw new InvalidOperationException("Assembly has no location");
                    }


                    var fileName = Path.GetFileName(assembly.Location);
                    var assemblyFolder = Path.GetDirectoryName(assembly.Location);

                    Directory.GetFiles(assemblyFolder, $"{Path.GetFileNameWithoutExtension(fileName)}.*").ToList()
                    .ForEach(file =>
                    {
                        file = Path.GetFileName(file);
                        File.Copy(Path.Combine(assemblyFolder, file), Path.Combine(destination, file), true);
                    });


                    return new[] { fileName };
                };

            return new TestPackage(assembly.GetName().Name, assembly.GetName().Version.ToString())
            {
                DependencySets =
                               new[]
                                   {
                                       standardDependencies,
                                       net46Dependencies
                                   },
                Extract =
                               extaction
            };
        }

        /// <summary>
        /// Gets the list of loaded assemblies
        /// </summary>
        /// <returns>The list of loaded assemblies</returns>
        private static IEnumerable<Assembly> GetLoadedAssemblies()
        {
#if APPDOMAIN
            var assemblies = new List<Assembly>();
            var currentDirectory = Path.GetFullPath(".");
            foreach (var file in Directory.GetFiles(currentDirectory, "*.dll"))
            {
                try
                {
                    assemblies.Add(Assembly.LoadFrom(file));
                }
                catch
                {
                    // ignore
                }
            }

            foreach (var file in Directory.GetFiles(currentDirectory, "*.exe"))
            {
                try
                {
                    assemblies.Add(Assembly.LoadFrom(file));
                }
                catch
                {
                    // ignore
                }
            }

            return assemblies;
#elif CORECLR
            var assemblies = new List<Assembly>();
            var dependencies = DependencyContext.Default.RuntimeLibraries;
            foreach (var library in dependencies)
            {
                try
                {
                    var assembly = Assembly.Load(new AssemblyName(library.Name));
                    assemblies.Add(assembly);
                }
                catch
                {
                    // do nothing can't if can't load assembly
                }
            }

            return assemblies;
#endif
        }
    }
}