// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   KlusterKite Node launcher
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.NodeManager.Launcher
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;

    using Akka.Configuration;

    using KlusterKite.NodeManager.Launcher.Messages;
    using KlusterKite.NodeManager.Launcher.Utils;

    using Newtonsoft.Json;

    using NuGet.Packaging.Core;
    using NuGet.Versioning;

    using RestSharp;
    using RestSharp.Authenticators;
    using RestSharp.Authenticators.OAuth2;

    /// <summary>
    /// KlusterKite Node launcher
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

            var config = File.Exists("config.hocon")
                             ? ConfigurationFactory.ParseString(File.ReadAllText("config.hocon"))
                             : Config.Empty;

            EnStopMode stopMode;
            if (Enum.TryParse(config.GetString("stopMode", EnStopMode.CleanRestart.ToString()), out stopMode))
            {
                this.StopMode = stopMode;
            }

            if (stopMode == EnStopMode.RunAction
                && string.IsNullOrWhiteSpace(config.GetString("stopAction")))
            {
                Console.WriteLine(@"stopAction is not configured");
                this.IsValid = false;
            }

            try
            {
                this.ConfigurationUrl = new Uri(config.GetString("nodeManagerUrl"));
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

            try
            {
                this.AuthenticationUrl = new Uri(config.GetString("authenticationUrl"));
            }
            catch (ArgumentNullException)
            {
                Console.WriteLine(@"authenticationUrl not configured");
                this.IsValid = false;
            }
            catch (UriFormatException)
            {
                Console.WriteLine(@"authenticationUrl is not a valid URL");
                this.IsValid = false;
            }

            this.WorkingDirectory = config.GetString("workingDirectory");
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

            if (!string.IsNullOrWhiteSpace(this.WorkingDirectory) && Directory.Exists(this.WorkingDirectory)
                && !CheckDirectoryAccess(this.WorkingDirectory))
            {
                Console.WriteLine(@"workingDirectory is not accessible");
                this.IsValid = false;
            }

            this.ContainerType = Environment.GetEnvironmentVariable("CONTAINER_TYPE")
                                 ?? config.GetString("containerType", "worker");

            this.Runtime = config.GetString("runtime");
            if (string.IsNullOrWhiteSpace(this.WorkingDirectory))
            {
                Console.WriteLine(@"containerType is not configured");
                this.IsValid = false;
            }

            this.ApiClientId = config.GetString("apiClientId");
            this.ApiClientSecret = config.GetString("apiClientSecret");
            
            if (string.IsNullOrWhiteSpace(this.ApiClientId) || string.IsNullOrWhiteSpace(this.ApiClientSecret))
            {
                Console.WriteLine(@"api access is not configured");
                this.IsValid = false;
            }

            this.FallBackConfiguration = config.GetString("fallbackConfiguration");
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
        /// Gets the authentication url
        /// </summary>
        public Uri AuthenticationUrl { get; }

        /// <summary>
        /// Gets the api authentication client id
        /// </summary>
        private string ApiClientId { get; }

        /// <summary>
        /// Gets the api authentication client secret
        /// </summary>
        private string ApiClientSecret { get; }

        /// <summary>
        /// Gets the url of cluster configuration service
        /// </summary>
        private Uri ConfigurationUrl { get; }

        /// <summary>
        /// Gets the type of container assigned to current machine
        /// </summary>
        private string ContainerType { get; }

        /// <summary>
        /// Gets the type of runtime
        /// </summary>
        private string Runtime { get; }

        /// <summary>
        /// Gets or sets the current api access token 
        /// </summary>
        private Token CurrentApiToken { get; set; }

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
        /// Gets the fallback configuration file
        /// </summary>
        private string FallBackConfiguration { get; }

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
        /// Removes all files and sub-directories from working directory
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

            var config = this.GetConfig();

            Console.WriteLine($@"Got {config.NodeTemplate} configuration");
            this.CleanWorkingDir();
            this.CreateService(config);
        }

        /// <summary>
        /// Gets the node config from the cluster
        /// </summary>
        /// <returns>The new config</returns>
        private NodeStartUpConfiguration GetConfig()
        {
            while (true)
            {
                if ((this.CurrentApiToken == null || this.CurrentApiToken.IsExpired) && !this.GetToken())
                {
                    return this.GetFallBackConfig();
                }

                var authenticator = new OAuth2AuthorizationRequestHeaderAuthenticator(
                    this.CurrentApiToken.AccessToken,
                    "Bearer");
                var options = new RestClientOptions(this.ConfigurationUrl) { Authenticator = authenticator, Timeout = new TimeSpan(0,0,5) };
                var client = new RestClient(options);

                var request = new RestRequest { Method = Method.Post };
                Console.WriteLine($"Requesting configuration for {this.ContainerType} with runtime {this.Runtime} and {PackageRepositoryExtensions.CurrentRuntime}");
                request.AddJsonBody(
                    new NewNodeTemplateRequest
                        {
                            ContainerType = this.ContainerType,
                            NodeUid = this.Uid,
                            FrameworkRuntimeType = PackageRepositoryExtensions.CurrentRuntime,
                            Runtime = this.Runtime
                        });

                var response = client.ExecuteAsync<NodeStartUpConfiguration>(request).GetAwaiter().GetResult();

                if (response.ResponseStatus != ResponseStatus.Completed
                    || response.StatusCode == HttpStatusCode.BadGateway)
                {
                    return this.GetFallBackConfig();
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Unexpected response (response code: {response.StatusCode}) from service");
                }

                return response.Data;
            }
        }

        /// <summary>
        /// Gets the fallback config in case of cluster unavailability
        /// </summary>
        /// <returns>The new config</returns>
        private NodeStartUpConfiguration GetFallBackConfig()
        {
            if (string.IsNullOrWhiteSpace(this.FallBackConfiguration) || !File.Exists(this.FallBackConfiguration))
            {
                throw new Exception("Could not get configuration from service and there is no fallback configuration");
            }

            Console.WriteLine(@"Could not get configuration, using fallback...");
            return JsonConvert.DeserializeObject<NodeStartUpConfiguration>(File.ReadAllText(this.FallBackConfiguration));
        }

        /// <summary>
        /// Gets the authentication token
        /// </summary>
        /// <returns>The success of network communication</returns>
        private bool GetToken()
        {
            this.CurrentApiToken = null;
            var options = new RestClientOptions(this.AuthenticationUrl) { Timeout = new TimeSpan(0, 0, 5) };
            var client = new RestClient(options);

            var request = new RestRequest { Method = Method.Post };
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("client_id", this.ApiClientId);
            request.AddParameter("client_secret", this.ApiClientSecret);

            while (true)
            {
                var result = client.ExecuteAsync(request).GetAwaiter().GetResult();
                if (result.ResponseStatus != ResponseStatus.Completed 
                    || result.StatusCode == HttpStatusCode.BadGateway 
                    || result.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }

                if (result.StatusCode == HttpStatusCode.OK)
                {
                    this.CurrentApiToken = JsonConvert.DeserializeObject<Token>(result.Content);
                    return true;
                }

                Console.WriteLine($@"!!!Failed to authenticate. Response status is {result.StatusCode}. Retrying in 60s");
                Thread.Sleep(TimeSpan.FromSeconds(60));
            }
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
            
            var repository = new RemotePackageRepository(configuration.PackageSource);
            repository.CreateServiceAsync(
                configuration.Packages.Select(p => new PackageIdentity(p.Id, NuGetVersion.Parse(p.Version))),
                this.Runtime,
                PackageRepositoryExtensions.CurrentRuntime,
                serviceDir,
                "KlusterKite.Core.Service",
                Console.WriteLine).GetAwaiter().GetResult();

            File.WriteAllText(Path.Combine(serviceDir, "akka.hocon"), configuration.Configuration);
            Console.WriteLine($@"General configuration: \n {configuration.Configuration}");

            // cluster self-join is not welcomed
            var seeds = configuration.Seeds?.ToList() ?? new List<string>();
            string startConfig = $@"{{
                KlusterKite.NodeManager.ConfigurationId = {configuration.ConfigurationId}
                KlusterKite.NodeManager.NodeTemplate = {configuration.NodeTemplate}
                KlusterKite.NodeManager.ContainerType = {this.ContainerType}
                KlusterKite.NodeManager.Runtime = ""{this.Runtime.Replace("\"", "\\\"")}""
                KlusterKite.NodeManager.NodeId = {this.Uid}
                akka.cluster.seed-nodes = [{string.Join(", ", seeds.Select(s => $"\"{s}\""))}]
            }}";
            File.WriteAllText(Path.Combine(serviceDir, "start.hocon"), startConfig);
            Console.WriteLine($@"Start configuration: 
                                {startConfig}");
        }

        /// <summary>
        /// Starts node software
        /// </summary>
        private void LaunchNode()
        {
            Console.WriteLine(@"Starting node");
#if APPDOMAIN
            var process = new Process
                              {
                                  StartInfo =
                                      {
                                          UseShellExecute = false,
                                          WorkingDirectory =
                                              Path.Combine(
                                                  Path.GetFullPath(this.WorkingDirectory),
                                                  "service"),
                                          FileName =
                                              Path.Combine(
                                                  Path.GetFullPath(this.WorkingDirectory),
                                                  "service",
                                                  "KlusterKite.Core.Service.exe"),
                                          Arguments = "--config=start.hocon"
                                      }
                              };
            process.Start();
#elif CORECLR
            var process = new Process
                              {
                                  StartInfo =
                                      {
                                          UseShellExecute = false,
                                          WorkingDirectory =
                                              Path.Combine(
                                                  Path.GetFullPath(this.WorkingDirectory),
                                                  "service"),
                                          FileName = "dotnet",
                                          Arguments = "KlusterKite.Core.Service.dll --config=start.hocon"
                                      }
                              };
            process.Start();
#endif

            process.WaitForExit();
            process.Dispose();
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
#if APPDOMAIN
                        catch (ThreadAbortException)
                        {
                            throw;
                        }
#endif
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