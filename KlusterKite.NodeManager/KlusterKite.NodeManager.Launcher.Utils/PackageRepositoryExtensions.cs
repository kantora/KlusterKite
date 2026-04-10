// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PackageRepositoryExtensions.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Extensions for <see cref="IPackageRepository" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Launcher.Utils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using NuGet.Packaging.Core;

    /// <summary>
    /// Extensions for <see cref="IPackageRepository"/>
    /// </summary>
    public static class PackageRepositoryExtensions
    {
        /// <summary>
        /// The .NET Framework 4.6 name
        /// </summary> 
        public const string Net46 = ".NETFramework,Version=v4.6";

        /// <summary>
        /// The .NET Core App 1.1 name
        /// </summary>
        public const string NetCore = ".NETCoreApp,Version=v1.1";

        /// <summary>
        /// The .NET 9.0 name
        /// </summary>
        public const string Net9 = ".NETCoreApp,Version=v9.0";

#if APPDOMAIN
        /// <summary>
        /// Gets the current runtime
        /// </summary>
        public static string CurrentRuntime => Net46;
#elif CORECLR
        /// <summary>
        /// Gets the current runtime
        /// </summary>
        public static string CurrentRuntime => Net9;
#endif

        /// <summary>
        /// Installs needed packages and configures the service
        /// </summary>
        /// <param name="repository">
        /// The nuget repository
        /// </param>
        /// <param name="packages">
        /// The list of packages
        /// </param>
        /// <param name="runtime">
        /// The current runtime
        /// </param>
        /// <param name="frameworkName">
        /// The runtime framework name
        /// </param>
        /// <param name="outputDir">
        /// The path to install service
        /// </param>
        /// <param name="executionFileName">
        /// The service execution file name to provide configuration files
        /// </param>
        /// <param name="logAction">The log writing action</param>
        /// <returns>
        /// The async task
        /// </returns>
        public static async Task CreateServiceAsync(
            this IPackageRepository repository,
            IEnumerable<PackageIdentity> packages,
            string runtime,
            string frameworkName,
            string outputDir,
            string executionFileName,
            Action<string> logAction = null)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var files = await repository.ExtractPackage(packages, runtime, frameworkName, outputDir, tempDir, logAction);
                ConfigurationUtils.FixAssemblyVersions(outputDir, executionFileName, files);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
