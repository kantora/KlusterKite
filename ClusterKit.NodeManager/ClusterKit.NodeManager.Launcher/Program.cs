﻿ // --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   ClusterKit Node launcher
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.NodeManager.Launcher
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Xml;

    using ClusterKit.NodeManager.Launcher.Messages;

    using Newtonsoft.Json;

    using NuGet.Common;
    using NuGet.Frameworks;

    using RestSharp;

    /// <summary>
    /// ClusterKit Node launcher
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Prevents a default instance of the <see cref="Program"/> class from being created.
        /// </summary>
        private Program()
        {
            if (!File.Exists("nuget.exe"))
            {
                Console.WriteLine(@"nuget.exe not found");
                this.IsValid = false;
            }

            EnStopMode stopMode;
            if (Enum.TryParse(ConfigurationManager.AppSettings["stopMode"], out stopMode))
            {
                this.StopMode = stopMode;
            }

            if (stopMode == EnStopMode.RunAction
                && string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["stopAction"]))
            {
                Console.WriteLine(@"stopAction is not configured");
                this.IsValid = false;
            }

            try
            {
                this.ConfigurationUrl = new Uri(ConfigurationManager.AppSettings["nodeManagerUrl"]);
            }
            catch (ArgumentNullException)
            {
                Console.WriteLine(@"nodeManagerUrl not configured");
                this.IsValid = false;
            }
            catch (UriFormatException)
            {
                Console.WriteLine(@"nodeManagerUrl is not a valid URL");
                this.IsValid = false;
            }

            this.WorkingDirectory = ConfigurationManager.AppSettings["workingDirectory"];
            if (string.IsNullOrWhiteSpace(this.WorkingDirectory))
            {
                Console.WriteLine(@"workingDirectory is not configured");
                this.IsValid = false;
            }
            else if (!Directory.Exists(this.WorkingDirectory))
            {
                try
                {
                    Directory.CreateDirectory(this.WorkingDirectory);
                }
                catch (Exception)
                {
                    Console.WriteLine(@"workingDirectory does not exist");
                    this.IsValid = false;
                }
            }

            if (!string.IsNullOrWhiteSpace(this.WorkingDirectory)
                && Directory.Exists(this.WorkingDirectory)
                && !CheckDirectoryAccess(this.WorkingDirectory))
            {
                Console.WriteLine(@"workingDirectory is not accessable");
                this.IsValid = false;
            }

            this.ContainerType = Environment.GetEnvironmentVariable("CONTAINER_TYPE") ?? ConfigurationManager.AppSettings["containerType"];
            if (string.IsNullOrWhiteSpace(this.WorkingDirectory))
            {
                Console.WriteLine(@"containerType is not configured");
                this.IsValid = false;
            }
        }

        /// <summary>
        /// Action to be done after node stops
        /// </summary>
        private enum EnStopMode
        {
            /// <summary>
            /// Will clean node data and request cluster for new node configuration
            /// </summary>
            CleanRestart,

            /// <summary>
            /// Will run action defined in Config
            /// </summary>
            RunAction
        }

        /// <summary>
        /// Gets the url of cluster configuration service
        /// </summary>
        private Uri ConfigurationUrl { get; }

        /// <summary>
        /// Gets the type of container assigned to current machine
        /// </summary>
        private string ContainerType { get; }

        /// <summary>
        /// Gets a value indicating whether that configuration is correct
        /// </summary>
        private bool IsValid { get; } = true;

        /// <summary>
        /// Gets the action to be done after node stops
        /// </summary>
        private EnStopMode StopMode { get; } = EnStopMode.CleanRestart;

        /// <summary>
        /// Gets current node unique identification number
        /// </summary>
        private Guid Uid { get; } = Guid.NewGuid();

        /// <summary>
        /// Gets the current working directory. All packages and new service will be stored here.
        /// </summary>
        private string WorkingDirectory { get; }

        /// <summary>
        /// Checks the ability to create and write to a file in the supplied directory.
        /// </summary>
        /// <param name="directory">String representing the directory path to check.</param>
        /// <returns>True if successful; otherwise false.</returns>
        private static bool CheckDirectoryAccess(string directory)
        {
            bool success = false;
            string fullPath = Path.Combine(directory, "tmp.tmp");

            if (Directory.Exists(directory))
            {
                try
                {
                    using (FileStream fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
                    {
                        fs.WriteByte(0xff);
                    }

                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        success = true;
                    }
                }
                catch (Exception)
                {
                    success = false;
                }
            }

            return success;
        }

        /// <summary>
        /// Launcher main entry point
        /// </summary>
        private static void Main()
        {
            var program = new Program();
            if (program.IsValid)
            {
                program.Start();
            }
        }

        /// <summary>
        /// Removes all files and subdirectories from working directory
        /// </summary>
        private void CleanWorkingDir()
        {
            foreach (var dir in Directory.GetDirectories(this.WorkingDirectory))
            {
                Directory.Delete(dir, true);
            }

            foreach (var file in Directory.GetFiles(this.WorkingDirectory))
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Prepares all node software for launch
        /// </summary>
        private void ConfigureNode()
        {
            Console.WriteLine(@"Configuring node...");

            var client = new RestClient(this.ConfigurationUrl);
            var request = new RestRequest { Method = Method.POST };
            request.AddBody(new NewNodeTemplateRequest { ContainerType = this.ContainerType, NodeUid = this.Uid });

            var response = client.Execute<NodeStartUpConfiguration>(request);
            NodeStartUpConfiguration config;

            while (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                var retryHeader = response.Headers.FirstOrDefault(
                    h => "Retry-After".Equals(h.Name, StringComparison.InvariantCultureIgnoreCase));
                int timeToWait;
                if (!int.TryParse(retryHeader?.Value?.ToString(), out timeToWait))
                {
                    timeToWait = 60;
                }

                Console.WriteLine($@"Config service anavailable, retrying in {timeToWait} seconds...");
                Thread.Sleep(TimeSpan.FromSeconds(timeToWait));
                response = client.Execute<NodeStartUpConfiguration>(request);
            }

            if (response.StatusCode != HttpStatusCode.OK || response.Data == null)
            {
                var fallBackConfiguration = ConfigurationManager.AppSettings["fallbackConfiguration"];
                if (string.IsNullOrWhiteSpace(fallBackConfiguration) || !File.Exists(fallBackConfiguration))
                {
                    throw new Exception(
                        $"Could not get configuration (response: {response.StatusCode}) from service and there is no fallback configuration");
                }

                Console.WriteLine(@"Could not get configuration, using fallback...");
                var serializer = new JsonSerializer();

                using (var file = File.OpenRead(fallBackConfiguration))
                using (var textReader = new StreamReader(file))
                using (var jsonReader = new JsonTextReader(textReader))
                {
                    config = serializer.Deserialize<NodeStartUpConfiguration>(jsonReader);
                }
            }
            else
            {
                config = response.Data;
            }

            Console.WriteLine($@"Got {config.NodeTemplate} configuration");
            this.CleanWorkingDir();
            this.PrepareNuGetConfig(config);
            this.InstallPackages(config);
            this.CreateService(config);
            this.FixAssemblyVersions(); 
        }

        /// <summary>
        /// Creates the runnable service from installed packages
        /// </summary>
        /// <param name="configuration">Current node configuration</param>
        private void CreateService(NodeStartUpConfiguration configuration)
        {
            Console.WriteLine(@"Creating service");
            var serviceDir = Path.Combine(this.WorkingDirectory, "service");
            Directory.CreateDirectory(serviceDir);

            var packageReader = new NuGet.Protocol.FindLocalPackagesResourcePackagesConfig(Path.Combine(this.WorkingDirectory, "packages"));
            var packages = packageReader.GetPackages(NullLogger.Instance, CancellationToken.None);

            var targetFramework = NuGetFramework.ParseFrameworkName(
                ".NETFramework,Version=v4.5",
                new DefaultFrameworkNameProvider());

            foreach (var localPackageInfo in packages)
            {
                using (var reader = localPackageInfo.GetReader())
                {
                    var specificGroup = NuGetFrameworkUtility.GetNearest(reader.GetLibItems(), targetFramework);
                    if (specificGroup == null || specificGroup.HasEmptyFolder)
                    {
                        continue;
                    }

                    foreach (var item in specificGroup.Items)
                    {
                        Console.WriteLine($@"Installing {localPackageInfo.Identity.Id} {item}");
                        var fileName = item.Split('/').Last();
                        using (var file = File.Create(Path.Combine(serviceDir, fileName)))
                        {
                            reader.GetStream(item).CopyTo(file);
                        }
                    }
                }
            }

            File.WriteAllText(Path.Combine(serviceDir, "akka.hocon"), configuration.Configuration);
            Console.WriteLine($@"General configuration: \n {configuration.Configuration}");

            // cluster self-join is not welcomed
            var seeds = configuration.Seeds.ToList();
            string startConfig = $@"{{
                ClusterKit.NodeManager.NodeTemplateVersion = {configuration.NodeTemplateVersion}
                ClusterKit.NodeManager.NodeTemplate = {configuration.NodeTemplate}
                ClusterKit.NodeManager.ContainerType = {this.ContainerType}
                ClusterKit.NodeManager.NodeId = { this.Uid }
                akka.cluster.seed-nodes = [{string.Join(", ", seeds.Select(s => $"\"{s}\""))}]
            }}";
            File.WriteAllText(Path.Combine(serviceDir, "start.hocon"), startConfig);
            Console.WriteLine($@"Start configuration: \n {startConfig}");
        }

        /// <summary>
        /// Fixes service configuration file to pass possible version conflicts in dependent assemblies
        /// </summary>
        private void FixAssemblyVersions()
        {
            var dirName = Path.Combine(this.WorkingDirectory, "service");
            var configName = Path.Combine(dirName, "ClusterKit.Core.Service.exe.config");

            XmlDocument confDoc = new XmlDocument();
            confDoc.Load(configName);
            var documentElement = confDoc.DocumentElement;
            if (documentElement == null)
            {
                Console.WriteLine($@"Configuration file {configName} is broken");
                return;
            }

            documentElement = (XmlElement)documentElement.SelectSingleNode("/configuration");
            if (documentElement == null)
            {
                Console.WriteLine($@"Configuration file {configName} is broken");
                return;
            }

            var runTimeNode = documentElement.SelectSingleNode("./runtime")
                              ?? documentElement.AppendChild(confDoc.CreateElement("runtime"));

            var nameTable = confDoc.NameTable;
            var namespaceManager = new XmlNamespaceManager(nameTable);
            const string Uri = "urn:schemas-microsoft-com:asm.v1";
            namespaceManager.AddNamespace("urn", Uri);

            var assemblyBindingNode = runTimeNode.SelectSingleNode("./urn:assemblyBinding", namespaceManager)
                                      ?? runTimeNode.AppendChild(confDoc.CreateElement("assemblyBinding", Uri));

            foreach (var lib in Directory.GetFiles(dirName, "*.dll"))
            {
                var parameters = AssemblyName.GetAssemblyName(lib);
                var dependentNode =
                    assemblyBindingNode?.SelectSingleNode($"./urn:dependentAssembly[./urn:assemblyIdentity/@name='{parameters.Name}']", namespaceManager)
                    ?? assemblyBindingNode?.AppendChild(confDoc.CreateElement("dependentAssembly", Uri));
                
                dependentNode.RemoveAll();
                var assemblyIdentityNode = (XmlElement)dependentNode.AppendChild(confDoc.CreateElement("assemblyIdentity", Uri));
                assemblyIdentityNode.SetAttribute("name", parameters.Name);
                assemblyIdentityNode.SetAttribute("publicKeyToken", BitConverter.ToString(parameters.GetPublicKeyToken()).Replace("-", "").ToLower(CultureInfo.InvariantCulture));
                var bindingRedirectNode = (XmlElement)dependentNode.AppendChild(confDoc.CreateElement("bindingRedirect", Uri));
                bindingRedirectNode.SetAttribute("oldVersion", $"0.0.0.0-{parameters.Version}");
                bindingRedirectNode.SetAttribute("newVersion", parameters.Version.ToString());

                // Console.WriteLine($"{parameters.Name} {parameters.Version} {BitConverter.ToString(parameters.GetPublicKeyToken()).Replace("-", "").ToLower(CultureInfo.InvariantCulture)}");
                Console.WriteLine($@"{parameters.Name} {parameters.Version}");
            }

            confDoc.Save(configName);
        }

        /// <summary>
        /// Download specified nuget packages and their dependencies
        /// </summary>
        /// <param name="configuration">The node configuration</param>
        private void InstallPackages(NodeStartUpConfiguration configuration)
        {
            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<packages>\n</packages>\n");
            // ReSharper disable PossibleNullReferenceException
            var rootNode = xmlDocument.DocumentElement.SelectSingleNode("/packages");
            foreach (var package in configuration.Packages)
            {
                var packageNode = (XmlElement)rootNode.AppendChild(xmlDocument.CreateElement("package"));
                packageNode.SetAttribute("id", package.Id);
                packageNode.SetAttribute("version", package.Version);
            }

            xmlDocument.Save(Path.Combine(this.WorkingDirectory, "packages.config"));
            using (var process = new Process())
            {
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.WorkingDirectory = Path.GetFullPath(this.WorkingDirectory);
                process.StartInfo.FileName = "nuget.exe";
                process.StartInfo.Arguments =
                    "install packages.config -PreRelease -NonInteractive -ConfigFile nuget.config -OutputDirectory packages -DisableParallelProcessing";
                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception("Could not install packages");
                }
            }

            //Checking installation
            var installedPackages = Directory.GetDirectories(Path.Combine(this.WorkingDirectory, "packages")).Select(Path.GetFileName).ToList();
            var missedPackages = configuration.Packages
                .Where(p => installedPackages.All(d => !Regex.IsMatch(d, $"^{Regex.Escape(p.Id)}(\\.\\d+){{0,4}}(\\-[\\w\\d\\-]*)?$")))
                .ToList();
            foreach (var packageName in missedPackages)
            {
                Console.WriteLine($@"Package {packageName.Id} was not installed");
            }

            if (missedPackages.Any())
            {
                throw new Exception("Could not install packages");
            }
        }

        /// <summary>
        /// Starts node software
        /// </summary>
        private void LaunchNode()
        {
            Console.WriteLine(@"Starting node");
            var process = new Process
                              {
                                  StartInfo =
                                      {
                                          UseShellExecute = false,
                                          WorkingDirectory =
                                              Path.Combine(Path.GetFullPath(this.WorkingDirectory), "service"),
                                          FileName =
                                              Path.Combine(
                                                  Path.GetFullPath(this.WorkingDirectory),
                                                  "service",
                                                  "ClusterKit.Core.Service.exe"),
                                          Arguments = "--config=start.hocon"
                                      }
                              };
            process.Start();

            process.WaitForExit();
            process.Dispose();
        }

        /// <summary>
        /// Sets NuGet configuration file from parameters
        /// </summary>
        /// <param name="configuration">Current node configuration</param>
        private void PrepareNuGetConfig(NodeStartUpConfiguration configuration)
        {
            var configPath = Path.Combine(this.WorkingDirectory, "nuget.config");

            File.Copy(
                // ReSharper disable once AssignNullToNotNullAttribute
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "nuget.exe"),
                Path.Combine(this.WorkingDirectory, "nuget.exe"));

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(Resources.NugetConfig);
            var root = doc.SelectSingleNode("/configuration/packageSources");

            int index = 0;
            foreach (var packageSource in configuration.PackageSources)
            {
                var addNode = doc.CreateElement("add");
                addNode.SetAttribute("key", $"s{index++}");
                addNode.SetAttribute("value", packageSource);
                root?.AppendChild(addNode);
            }

            doc.Save(configPath);
        }

        /// <summary>
        /// Starts the new session
        /// </summary>
        private void Start()
        {
            switch (this.StopMode)
            {
                case EnStopMode.CleanRestart:
                    while (true)
                    {
                        // this is infinity cycle. LaunchNode will exit on node stop. This happens on manual or automatic system shutdown or major exception. In both cases node will be reinitiated.
                        try
                        {
                            this.ConfigureNode();
                            this.LaunchNode();
                            Thread.Sleep(TimeSpan.FromSeconds(30));
                        }
                        catch (ThreadAbortException)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine($@"Failed to launch, {exception.Message}");
                            Console.WriteLine(exception.StackTrace);
                            Thread.Sleep(TimeSpan.FromSeconds(10));
                        }
                    }
                default:
                    Console.WriteLine(@"Unsupported stop mode");
                    return;
            }
        }
    }
}