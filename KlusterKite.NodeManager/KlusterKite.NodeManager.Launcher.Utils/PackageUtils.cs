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

    using System.Net.Http;
    using System.Xml.Linq;

    using NuGet.Client;
    using NuGet.Common;
    using NuGet.Configuration;
    using NuGet.ContentModel;
    using NuGet.Frameworks;
    using NuGet.Packaging;
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
        /// Shared HTTP client for V2 feed queries.
        /// </summary>
        private static readonly HttpClient HttpClient = new HttpClient();

        /// <summary>
        /// Gets the latest version of a package by exact ID using the V2 FindPackagesById endpoint directly.
        /// More reliable than text search for packages whose IDs don't match search tokenization
        /// (e.g. StackExchange.Redis, Serilog).
        /// </summary>
        /// <param name="nugetUrl">The nuget server address (V2)</param>
        /// <param name="id">The exact package ID</param>
        /// <returns>The package metadata, or null if not found</returns>
        public static async Task<IPackageSearchMetadata> GetByIdAsync(string nugetUrl, string id)
        {
            var baseUrl = nugetUrl.TrimEnd('/');
            var url = $"{baseUrl}/FindPackagesById()?id='{Uri.EscapeDataString(id)}'";
            var response = await HttpClient.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var xml = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var doc = XDocument.Parse(xml);
            XNamespace d = "http://schemas.microsoft.com/ado/2007/08/dataservices";
            XNamespace m = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
            XNamespace atom = "http://www.w3.org/2005/Atom";

            // Pick the latest version from all entries in the feed
            var entry = doc.Descendants(atom + "entry")
                .Select(e => new { e, v = NuGetVersion.TryParse(e.Element(m + "properties")?.Element(d + "NormalizedVersion")?.Value ?? e.Element(m + "properties")?.Element(d + "Version")?.Value ?? string.Empty, out var ver) ? ver : null })
                .Where(x => x.v != null)
                .OrderByDescending(x => x.v)
                .FirstOrDefault()?.e;
            if (entry == null)
            {
                return null;
            }

            var props = entry.Element(m + "properties");
            if (props == null)
            {
                return null;
            }

            var packageId = props.Element(d + "Id")?.Value ?? entry.Element(atom + "title")?.Value ?? id;
            var versionStr = props.Element(d + "NormalizedVersion")?.Value ?? props.Element(d + "Version")?.Value;
            if (versionStr == null || !NuGetVersion.TryParse(versionStr, out var version))
            {
                return null;
            }

            var dependencyStr = props.Element(d + "Dependencies")?.Value ?? string.Empty;
            var dependencySets = ParseV2DependencyString(dependencyStr);

            return new V2DirectMetadata(new PackageIdentity(packageId, version), dependencySets);
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
            // Same V2-direct path as GetByIdAsync, but filter by exact version
            // instead of picking the latest. The NuGet client's
            // PackageSearchResource is unreliable against the local
            // simple-nuget-server (its V2 service-index probe fails with
            // "Unable to load the service index for source ...").
            var baseUrl = nugetUrl.TrimEnd('/');
            var url = $"{baseUrl}/FindPackagesById()?id='{Uri.EscapeDataString(id)}'";
            var response = await HttpClient.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var xml = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var doc = XDocument.Parse(xml);
            XNamespace d = "http://schemas.microsoft.com/ado/2007/08/dataservices";
            XNamespace m = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
            XNamespace atom = "http://www.w3.org/2005/Atom";

            var entry = doc.Descendants(atom + "entry")
                .Select(e => new
                {
                    e,
                    v = NuGetVersion.TryParse(
                            e.Element(m + "properties")?.Element(d + "NormalizedVersion")?.Value
                            ?? e.Element(m + "properties")?.Element(d + "Version")?.Value
                            ?? string.Empty,
                            out var ver)
                        ? ver
                        : null
                })
                .FirstOrDefault(x => x.v != null && x.v == version)?.e;
            if (entry == null)
            {
                return null;
            }

            var props = entry.Element(m + "properties");
            if (props == null)
            {
                return null;
            }

            var packageId = props.Element(d + "Id")?.Value ?? entry.Element(atom + "title")?.Value ?? id;
            var dependencyStr = props.Element(d + "Dependencies")?.Value ?? string.Empty;
            var dependencySets = ParseV2DependencyString(dependencyStr);

            return new V2DirectMetadata(new PackageIdentity(packageId, version), dependencySets);
        }

        /// <summary>
        /// Get the list of package descriptions
        /// </summary>
        /// <param name="repository">The package repository</param>
        /// <param name="terms">The search terms</param>
        /// <returns>The list of package descriptions</returns>
        public static async Task<List<IPackageSearchMetadata>> Search(string repository, string terms)
        {
            var source = new PackageSource(repository) { AllowInsecureConnections = true };
            var sourceRepository = Repository.Factory.GetCoreV3(source);
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
        /// <summary>
        /// Parses the NuGet V2 dependency string format: "id:version:framework|id:version:framework|..."
        /// into a list of PackageDependencyGroup objects.
        /// </summary>
        private static IEnumerable<PackageDependencyGroup> ParseV2DependencyString(string dependencyStr)
        {
            if (string.IsNullOrWhiteSpace(dependencyStr))
            {
                yield break;
            }

            // Group entries by framework
            var byFramework = new Dictionary<string, List<PackageDependency>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in dependencyStr.Split('|'))
            {
                var parts = entry.Split(':');
                if (parts.Length < 1 || string.IsNullOrWhiteSpace(parts[0]))
                {
                    continue;
                }

                var depId = parts[0];
                VersionRange range = null;
                if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    VersionRange.TryParse(parts[1], out range);
                }

                var framework = parts.Length >= 3 ? parts[2] : string.Empty;
                if (!byFramework.TryGetValue(framework, out var list))
                {
                    list = new List<PackageDependency>();
                    byFramework[framework] = list;
                }

                list.Add(new PackageDependency(depId, range));
            }

            foreach (var kv in byFramework)
            {
                NuGetFramework fw;
                if (string.IsNullOrEmpty(kv.Key))
                {
                    fw = NuGetFramework.AnyFramework;
                }
                else
                {
                    // V2 framework moniker uses short form e.g. "net90", "netcoreapp31"
                    fw = NuGetFramework.ParseFolder(kv.Key);
                    if (fw == NuGetFramework.UnsupportedFramework)
                    {
                        fw = NuGetFramework.ParseFrameworkName(kv.Key, DefaultFrameworkNameProvider.Instance);
                    }
                }

                yield return new PackageDependencyGroup(fw, kv.Value);
            }
        }

        /// <summary>
        /// Minimal IPackageSearchMetadata backed by data parsed directly from V2 Atom feed.
        /// Only Identity and DependencySets are meaningful; all other members return defaults.
        /// </summary>
        private class V2DirectMetadata : IPackageSearchMetadata
        {
            private readonly IEnumerable<PackageDependencyGroup> dependencySets;

            public V2DirectMetadata(PackageIdentity identity, IEnumerable<PackageDependencyGroup> dependencySets)
            {
                this.Identity = identity;
                this.dependencySets = dependencySets.ToList();
            }

            public PackageIdentity Identity { get; }

            public IEnumerable<PackageDependencyGroup> DependencySets => this.dependencySets;

            public string Authors => null;
            public string Description => null;
            public long? DownloadCount => null;
            public Uri IconUrl => null;
            public bool IsListed => true;
            public LicenseMetadata LicenseMetadata => null;
            public Uri LicenseUrl => null;
            public string Owners => null;
            public Uri PackageDetailsUrl => null;
            public bool PrefixReserved => false;
            public Uri ProjectUrl => null;
            public DateTimeOffset? Published => null;
            public Uri ReadmeUrl => null;
            public Uri ReportAbuseUrl => null;
            public bool RequireLicenseAcceptance => false;
            public string Summary => null;
            public string Tags => null;
            public string Title => null;
            public IEnumerable<PackageVulnerabilityMetadata> Vulnerabilities => null;
            public string ReadmeFileUrl => null;
            public IReadOnlyList<string> OwnersList => null;

            public Task<PackageDeprecationMetadata> GetDeprecationMetadataAsync() => Task.FromResult<PackageDeprecationMetadata>(null);
            public Task<IEnumerable<VersionInfo>> GetVersionsAsync() => Task.FromResult(Enumerable.Empty<VersionInfo>());
        }

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