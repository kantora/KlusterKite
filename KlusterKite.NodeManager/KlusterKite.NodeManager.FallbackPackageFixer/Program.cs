// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Fixes the list of packages for dependencies in fall-back files
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.FallbackPackageFixer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using JetBrains.Annotations;

    using KlusterKite.NodeManager.Launcher.Messages;

    using Newtonsoft.Json;

    using NuGet.Common;
    using NuGet.Packaging;
    using NuGet.Protocol;

    /// <summary>
    /// Fixes the list of packages for dependencies in fall-back files
    /// </summary>
    [UsedImplicitly]
    public class Program
    {
        /// <summary>
        /// Main entry point
        /// </summary>
        /// <param name="args">List of command line arguments</param>
        private static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine($"Usage: {Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName)}"
                                  + " [fallback.json] [package directories...]");
                return;
            }

            var fileName = args[0];
            if (!File.Exists(fileName))
            {
                Console.WriteLine(Path.GetFullPath(fileName));
                Console.WriteLine($"Could not find file {fileName}");
                return;
            }

            foreach (var packageDirectory in args.Skip(1).Where(packageDirectory => !Directory.Exists(packageDirectory)))
            {
                Console.WriteLine($"Could not find package directory {packageDirectory}");
                return;
            }

            var description = JsonConvert.DeserializeObject<NodeStartUpConfiguration>(File.ReadAllText(fileName));

            var installedMetadata = args.Skip(1).SelectMany(
                d => LocalFolderUtility.GetPackagesV2(Path.GetFullPath(d), NullLogger.Instance).Select(p => p.Nuspec))
                .ToList();

            Console.WriteLine("Installed packages: ");
            foreach (var package in installedMetadata)
            {
                Console.WriteLine($"\t{package.GetId()} {package.GetVersion()}");
            }

            var localPackages = installedMetadata
                .Where(rp => description.Packages.Any(lp => lp.Id == rp.GetId()))
                .GroupBy(p => p.GetId()).Select(g => g.OrderByDescending(p => p.GetVersion()).First())
                .ToList();

            Console.WriteLine("Packages found: ");
            foreach (var package in localPackages)
            {
                Console.WriteLine($"\t{package.GetId()} {package.GetVersion()}");
            }

            Console.WriteLine(string.Empty);
            var packagesList = new List<NuspecReader>(localPackages);

            while (true)
            {
                var allDependencies = packagesList.SelectMany(p => p.GetDependencyGroups()).SelectMany(p => p.Packages)
                    .OrderBy(d => d.Id).ToList();

                var additionalPackages = allDependencies
                    .SelectMany(d => installedMetadata.Where(p => p.GetId() == d.Id && d.VersionRange.Satisfies(p.GetVersion())))
                    .GroupBy(p => p.GetId()).Select(g => g.OrderBy(p => p.GetVersion()).First())
                    .Where(np => packagesList.All(p => p.GetId() != np.GetId())).ToList();

                packagesList.AddRange(additionalPackages);
                if (additionalPackages.Count == 0)
                {
                    break;
                }
            }

            var result = packagesList
                .Select(p => new PackageDescription { Id = p.GetId(), Version = p.GetVersion().ToString() })
                .OrderBy(p => p.Id)
                .ToList();

            description.Packages = result;
            File.WriteAllText(fileName, JsonConvert.SerializeObject(description, Formatting.Indented));

            Console.WriteLine("Packages: ");
            foreach (var package in result)
            {
                Console.WriteLine($"\t{package.Id} {package.Version}");
            }
        }
    }
}