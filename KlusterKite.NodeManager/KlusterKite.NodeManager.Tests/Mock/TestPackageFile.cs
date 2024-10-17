// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestPackageFile.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   The test package file  representation
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Tests.Mock
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Versioning;
    using NuGet.Frameworks;
    using NuGet.Packaging;

    /// <summary>
    /// The test package file  representation
    /// </summary>
    internal class TestPackageFile : IPackageFile
    {
        /// <inheritdoc />
        public string EffectivePath { get; set; }

        /// <summary>
        /// Gets or sets a delegate for <see cref="IPackageFile.GetStream"/>
        /// </summary>
        public Func<Stream> GetStreamAction { get; set; }

        /// <inheritdoc />
        public string Path { get; set; }

        /// <inheritdoc />
        public IEnumerable<FrameworkName> SupportedFrameworks { get; set; }

        /// <inheritdoc />
        public FrameworkName TargetFramework { get; set; }

        public NuGetFramework NuGetFramework { get; set; }

        public DateTimeOffset LastWriteTime { get; set; }

        /// <inheritdoc />
        public Stream GetStream()
        {
            if (this.GetStreamAction == null)
            {
                throw new InvalidOperationException();
            }

            return this.GetStreamAction();
        }
    }
}