// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestPackage.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The test package representation
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Tests.Mock
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    using NuGet.Packaging;
    using NuGet.Packaging.Core;
    using NuGet.Protocol;
    using NuGet.Protocol.Core.Types;
    using NuGet.Versioning;

    /// <summary>
    /// The test package representation
    /// </summary>
    [SuppressMessage("ReSharper", "UnassignedGetOnlyAutoProperty", Justification = "This is test implementation")]
    internal class TestPackage : IPackageSearchMetadata
    {
        /// <inheritdoc />
        public TestPackage(string id, string version)
        {
            this.Identity = new PackageIdentity(id, NuGetVersion.Parse(version));
            this.DependencySets = new List<PackageDependencyGroup>();
        }

        /// <inheritdoc />
        public string Authors { get; }

        /// <inheritdoc />
        public IEnumerable<PackageDependencyGroup> DependencySets { get; set; }

        /// <inheritdoc />
        public string Description { get; }

        /// <inheritdoc />
        public long? DownloadCount { get; }

        /// <summary>
        /// Gets or sets the extract action
        /// </summary>
        public Func<string, string, string, IEnumerable<string>> Extract { get; set; }

        /// <inheritdoc />
        public Uri IconUrl { get; }

        /// <inheritdoc />
        public PackageIdentity Identity { get; set; }

        /// <inheritdoc />
        public bool IsListed { get; }

        /// <inheritdoc />
        public Uri LicenseUrl { get; }

        /// <inheritdoc />
        public string Owners { get; }

        /// <inheritdoc />
        public Uri ProjectUrl { get; }

        /// <inheritdoc />
        public DateTimeOffset? Published { get; }

        /// <inheritdoc />
        public Uri ReportAbuseUrl { get; }

        /// <inheritdoc />
        public bool RequireLicenseAcceptance { get; }

        /// <inheritdoc />
        public string Summary { get; }

        /// <inheritdoc />
        public string Tags { get; }

        /// <inheritdoc />
        public string Title { get; }

        public Uri ReadmeUrl { get; }

        public Uri PackageDetailsUrl { get; }

        public IReadOnlyList<string> OwnersList { get; }

        public bool PrefixReserved { get; }

        public LicenseMetadata LicenseMetadata { get; }

        public IEnumerable<PackageVulnerabilityMetadata> Vulnerabilities { get; }

        public Task<PackageDeprecationMetadata> GetDeprecationMetadataAsync()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<IEnumerable<VersionInfo>> GetVersionsAsync()
        {
            throw new NotImplementedException();
        }
    }
}