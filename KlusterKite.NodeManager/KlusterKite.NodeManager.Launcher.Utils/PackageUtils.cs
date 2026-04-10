// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PackageUtils.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Helper methods to work with nuget packages
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Launcher.Utils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using NuGet.Client;
    using NuGet.Common;
    using NuGet.Configuration;
    using NuGet.ContentModel;
    using NuGet.Frameworks;
    using NuGet.Packaging.Core;
    using NuGet.Protocol;
    using NuGet.Protocol.Core.Types;
    using NuGet.RuntimeModel;
    using NuGet.Versioning;

    /// <summary>
    /// Helper methods to work with nuget packages
    /// </summary>
    public static class PackageUtils
    {
        /// <summary>
        /// The global runtime graph
        /// </summary>
        private static RuntimeGraph runtimeGraph;

        /// <summary>
        /// Initializes static members of the <see cref="PackageUtils"/> class.
        /// </summary>
        static PackageUtils()
        {
            // the original data was taken from: https://github.com/dotnet/corefx/blob/master/pkg/Microsoft.NETCore.Platforms/runtime.json
            // the additional documentation: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog
            runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(
                typeof(PackageUtils).GetTypeInfo().Assembly
                    .GetManifestResourceStream("KlusterKite.NodeManager.Launcher.Utils.Resources.runtimes.json"));
        }

        /// <summary>
        /// Installs specified packages to specified place
        /// </summary>
        /// <param name="packages">
        /// The list of packages to install
        /// </param>
        /// <param name="runtime">
        /// The current runtime
        /// </param>
        /// <param name="framework">
        /// The target framework to install
        /// </param>
        /// <param name="nugetUrl">
        /// The url of nuget repository
        /// </param>
        /// <param name="directoryToInstall">
        /// The directory to install packages contents
        /// </param>
        /// <param name="logAction">The log writing action</param>
        /// <returns>
        /// The list of installed files for each package
        /// </returns>
        public static async Task<Dictionary<PackageIdentity, IEnumerable<string>>> Install(
            this IEnumerable<PackageIdentity> packages,
            string runtime,
            NuGetFramework framework,
            string nugetUrl,
            string directoryToInstall,
            Action<string> logAction = null)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var source = new PackageSource(nugetUrl) { AllowInsecureConnections = true };
            var sourceRepository = Repository.Factory.GetCoreV3(source);
            var sourceCacheContext = new SourceCacheContext();
            
            var downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>().ConfigureAwait(false);
            var packageDownloadContext = new PackageDownloadContext(sourceCacheContext);
            var result = new Dictionary<PackageIdentity, IEnumerable<string>>();
            try
            {
                var downloadTasks = packages.Select(
                    package => downloadResource.GetDownloadResourceResultAsync(
                        package,
                        packageDownloadContext,
                        tempDir,
                        NullLogger.Instance,
                        CancellationToken.None).ContinueWith(
                        t =>
                            {
                                logAction?.Invoke(
                                    t.Result.PackageReader == null
                                        ? $"{package.Id} {package.Version} failed to download: {t.Result.Status}"
                                        : $"{package.Id} downloaded");
                                return t.Result;
                            }));

                var downloads = await Task.WhenAll(downloadTasks);

                if (downloads.Any(d => d.PackageReader == null))
                {
                    throw new Exception("Could not install all required packages");
                }

                var tools = downloads.Where(d => d.PackageReader.GetToolItems().Any()).ToList();
                var libraries = downloads.Where(d => !d.PackageReader.GetToolItems().Any()).ToList();

                foreach (var package in tools.OrderBy(d => d.PackageReader.GetIdentity().Id))
                {
                    var files = ExtractPackage(package, runtime, framework, directoryToInstall).ToList();
                    logAction?.Invoke($"{package.PackageReader.GetIdentity()} extracted as tool with {files.Count} files");
                    result[package.PackageReader.GetIdentity()] = files;
                }

                foreach (var package in libraries.OrderBy(d => d.PackageReader.GetIdentity().Id))
                {
                    var files = ExtractPackage(package, runtime, framework, directoryToInstall).ToList();
                    logAction?.Invoke($"{package.PackageReader.GetIdentity()} extracted as lib with {files.Count} files");
                    result[package.PackageReader.GetIdentity()] = files;
                }

                return result;
            }
            finally
            {
                try
                {
                    GC.Collect();
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // ignore
                }
            }
        }

        /// <summary>
        /// Searches for a specific package
        /// </summary>
        /// <param name="nugetUrl">
        /// The nuget server address
        /// </param>
        /// <param name="id">
        /// The package id
        /// </param>
        /// <param name="version">
        /// The package version.
        /// </param>
        /// <returns>
        /// The package metadata
        /// </returns>
        public static async Task<IPackageSearchMetadata> Search(string nugetUrl, string id, NuGetVersion version)
        {
            var source = new PackageSource(nugetUrl) { AllowInsecureConnections = true };
            var sourceRepository = Repository.Factory.GetCoreV3(source);
            var resource = await sourceRepository.GetResourceAsync<PackageSearchResource>().ConfigureAwait(false);
            var result = new List<IPackageSearchMetadata>();

            const int PageSize = 1000;
            var position = 0;
            while (true)
            {
                var searchMetadata = (await resource.SearchAsync(
                                         id,
                                         new SearchFilter(true, null),
                                         position,
                                         PageSize,
                                         NullLogger.Instance,
                                         CancellationToken.None)).ToList();
                var previousCount = result.Count;
                result.AddRange(searchMetadata);
                position += PageSize;
                if (searchMetadata.Any(s => s.Identity.Id == id))
                {
                    break;
                }

                if (result.Count - previousCount < PageSize)
                {
                    break;
                }
            }

            var package = result.FirstOrDefault(s => s.Identity.Id == id);
            if (package == null)
            {
                return null;
            }

            var versions = (await package.GetVersionsAsync()).ToList();
            return versions.FirstOrDefault(v => v.Version == version)?.PackageSearchMetadata;
        }

        /// <summary>
        /// Get the list of package descriptions
        /// </summary>
        /// <param name="repository">The package repository</param>
        /// <param name="terms">The search terms</param>
        /// <returns>The list of package descriptions</returns>
        public static async Task<List<IPackageSearchMetadata>> Search(string repository, string terms)
        {
            var source = new PackageSource(repository);
            var sourceRepository = Repository.Factory.GetCoreV3(source.Source);
            var resource = await sourceRepository.GetResourceAsync<PackageSearchResource>().ConfigureAwait(false);
            var result = new List<IPackageSearchMetadata>();

            const int PageSize = 1000;
            var position = 0;
            while (true)
            {
                var searchMetadata = await resource.SearchAsync(
                                          terms,
                                          new SearchFilter(true, null),
                                          position,
                                          PageSize,
                                          NullLogger.Instance,
                                          CancellationToken.None);
                var previousCount = result.Count;
                result.AddRange(searchMetadata);
                position += PageSize;
                if (result.Count - previousCount < PageSize)
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts the lib files to execution directory
        /// </summary>
        /// <param name="package">
        /// The package to extract
        /// </param>
        /// <param name="runtime">
        /// The current runtime
        /// </param>
        /// <param name="frameworkName">
        /// The current framework name
        /// </param>
        /// <param name="executionDir">
        /// The execution directory to load packages
        /// </param>
        /// <param name="logAction">The log writing action</param>
        /// <returns>
        /// The list of extracted files
        /// </returns>
        private static IEnumerable<string> ExtractPackage(
            DownloadResourceResult package,
            string runtime,
            NuGetFramework frameworkName,
            string executionDir,
            Action<string> logAction = null)
        {
            try
            {
                var id = package.PackageReader.GetIdentity();
                var files = NuGetFrameworkUtility.GetNearest(package.PackageReader.GetLibItems(), frameworkName)?.Items.ToList()
                            ?? NuGetFrameworkUtility.GetNearest(package.PackageReader.GetToolItems(), frameworkName)?.Items.ToList();

                if (files == null || files.Count == 0)
                {
                    var collection = new ContentItemCollection();
                    collection.Load(package.PackageReader.GetFiles());

                    var conventions = new ManagedCodeConventions(runtimeGraph);
                    var criteria = conventions.Criteria.ForFrameworkAndRuntime(
                        NuGetFramework.ParseFrameworkName(
                            PackageRepositoryExtensions.CurrentRuntime,
                            DefaultFrameworkNameProvider.Instance),
                        runtime);

                    files = collection.FindBestItemGroup(criteria, conventions.Patterns.NativeLibraries)?.Items
                        .Select(i => i.Path).ToList();
                    if (files == null || files.Count == 0)
                    {
                        files = collection.FindBestItemGroup(criteria, conventions.Patterns.RuntimeAssemblies)?.Items
                            .Select(i => i.Path).ToList();
                    }

                    if (files == null || files.Count == 0)
                    {
                        return new string[0];
                    }
                    else
                    {
                        logAction?.Invoke($"{id.Id}: {string.Join(", ", files)}");
                    }
                }

                foreach (var file in files)
                {
                    using (var fileStream = File.Create(Path.Combine(executionDir, Path.GetFileName(file) ?? file)))
                    {
                        package.PackageReader.GetStream(file).CopyTo(fileStream);
                    }
                }

                return files.Select(file => Path.GetFileName(file) ?? file);
            }
            finally
            {
                package.Dispose();
            }
        }
    }
}