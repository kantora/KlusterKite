// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NginxConfiguratorActor.cs" company="KlusterKite">
//   All rights reserved
// </copyright>
// <summary>
//   Follows cluster changes for adding / removing new nodes with "web" role and configures local nginx for supported urls
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace KlusterKite.Web.NginxConfigurator
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;

    using Akka.Actor;
    using Akka.Cluster;
    using Akka.Configuration;
    using Akka.Event;

    using JetBrains.Annotations;

    using KlusterKite.Web.Client;
    using KlusterKite.Web.Client.Messages;

    /// <summary>
    /// Follows cluster changes for adding / removing new nodes with "web" role and configures local nginx for supported services
    /// </summary>
    [UsedImplicitly]
    public class NginxConfiguratorActor : ReceiveActor
    {
        /// <summary>
        /// Current configuration file path
        /// </summary>
        private readonly string configPath;

        /// <summary>
        /// Nginx configuration reload command
        /// </summary>
        private readonly Config reloadCommand;

        /// <summary>
        /// Initializes a new instance of the <see cref="NginxConfiguratorActor"/> class.
        /// </summary>
        /// <summary>Sent to self on a schedule to retry pending Web nodes that haven't responded yet.</summary>
        private sealed class RetryPendingNodes { public static readonly RetryPendingNodes Instance = new RetryPendingNodes(); }

        public NginxConfiguratorActor()
        {
            this.configPath = Context.System.Settings.Config.GetString("KlusterKite.Web.Nginx.PathToConfig");
            this.reloadCommand = Context.System.Settings.Config.GetConfig("KlusterKite.Web.Nginx.ReloadCommand");
            this.InitFromConfiguration();

            Cluster.Get(Context.System)
                .Subscribe(
                    this.Self,
                    ClusterEvent.InitialStateAsEvents,
                    new[] { typeof(ClusterEvent.MemberUp), typeof(ClusterEvent.MemberRemoved) });

            this.Receive<ClusterEvent.MemberUp>(
                m => m.Member.Roles.Contains("Web"),
                m => this.OnWebNodeUp(m.Member.Address));

            this.Receive<ClusterEvent.MemberRemoved>(
                m => m.Member.Roles.Contains("Web"),
                m => this.OnWebNodeDown(m.Member.Address));

            this.Receive<WebDescriptionResponse>(r => this.OnNewNodeDescription(r));

            // Retry any Web nodes that joined but whose WebDescriptorActor hadn't started yet
            // when we first sent the request. On slow/low-CPU machines the WebDescriptorActor
            // can take several seconds longer to initialize than the MemberUp gossip takes to
            // propagate, so the first request dead-letters. Retry every 15 s until answered.
            this.Receive<RetryPendingNodes>(_ => this.RetryPending());
            Context.System.Scheduler.ScheduleTellRepeatedly(
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(15),
                this.Self,
                RetryPendingNodes.Instance,
                this.Self);
        }

        /// <summary>
        /// Gets nodes configuration description
        /// </summary>
        public WebConfiguration Configuration { get; } = new WebConfiguration();

        /// <summary>
        /// Gets the list of known active web nodes addresses
        /// </summary>
        public List<Address> KnownActiveNodes { get; } = new List<Address>();

        /// <summary>
        /// Gets cached data of published web urls in every known node
        /// </summary>
        public Dictionary<Address, WebDescriptionResponse> NodePublishUrls { get; } = new Dictionary<Address, WebDescriptionResponse>();

        /// <summary>
        /// Compiles upstream name from hostname and service name
        /// </summary>
        /// <param name="hostName">Host name of service</param>
        /// <param name="serviceName">Service location</param>
        /// <returns>The corresponding upstream name</returns>
        private string GetUpStreamName([NotNull] string hostName, [NotNull] string serviceName)
        {
            if (hostName == null)
            {
                throw new ArgumentNullException(nameof(hostName));
            }

            if (serviceName == null)
            {
                throw new ArgumentNullException(nameof(serviceName));
            }

            return $"KlusterKiteWeb_{hostName.Replace('.', '_')}_{serviceName.Replace('/', '_').Replace('.', '_')}";
        }

        /// <summary>
        /// Initialized base nginx configuration from self configuration
        /// </summary>
        private void InitFromConfiguration()
        {
            var config = Context.System.Settings.Config.GetConfig("KlusterKite.Web.Nginx.Configuration");
            if (config == null)
            {
                return;
            }

            foreach (var pair in config.AsEnumerable())
            {
                var hostName = pair.Key;
                this.InitHostFromConfiguration(hostName, pair.Value.AtKey("Key").GetConfig("Key"));
            }
        }

        /// <summary>
        /// Initializes nginx server configuration from self configuration
        /// </summary>
        /// <param name="hostName">Local host identification</param>
        /// <param name="config">Section of self configuration, dedicated for the host configuration</param>
        private void InitHostFromConfiguration(string hostName, Config config)
        {
            StringBuilder hostConfig = new StringBuilder();
            foreach (var parameter in config.AsEnumerable())
            {
                if (parameter.Value.IsString())
                {
                    hostConfig.AppendFormat("\t{0} {1};\n", parameter.Key, parameter.Value.GetString());
                }

                if (parameter.Value.IsObject()
                    && parameter.Key.StartsWith("location ", StringComparison.OrdinalIgnoreCase))
                {
                    var serviceName = parameter.Key.Substring("location ".Length).Trim();
                    this.InitServiceFromConfiguration(
                        this.Configuration[hostName],
                        serviceName,
                        parameter.Value.AtKey("Key").GetConfig("Key"));
                }
            }

            this.Configuration[hostName].Config = hostConfig.ToString();
        }

        /// <summary>
        /// Initializes nginx location configuration from self configuration
        /// </summary>
        /// <param name="host">The parent server configuration</param>
        /// <param name="serviceName">Location name</param>
        /// <param name="config">Section of self configuration, dedicated for the service configuration</param>
        private void InitServiceFromConfiguration(HostConfiguration host, string serviceName, Config config)
        {
            StringBuilder serviceConfig = new StringBuilder();
            foreach (var parameter in config.AsEnumerable())
            {
                if (parameter.Value.IsString())
                {
                    serviceConfig.AppendFormat("\t\t{0} {1};\n", parameter.Key, parameter.Value.GetString());
                }
                else if (parameter.Value.IsArray())
                {
                    var hoconValues = parameter.Value.GetArray();
                    foreach (var hoconValue in hoconValues)
                    {
                        serviceConfig.AppendFormat("\t\t{0} {1};\n", parameter.Key, hoconValue.GetString());
                    }
                }
            }

            // proxy_set_header Host $http_host;
            var headers = config.GetValue("proxy_set_header");
            if (headers == null
                || (headers.IsString()
                    && (headers.GetString() ?? string.Empty).ToLower()
                           .IndexOf("Host ", StringComparison.OrdinalIgnoreCase) < 0)
                || (headers.IsArray()
                    && headers.GetArray()
                           .Select(v => v.GetString())
                           .All(
                               v =>
                               (v ?? string.Empty).ToLower().IndexOf("Host ", StringComparison.OrdinalIgnoreCase) < 0)))
            {
                serviceConfig.Append("\t\tproxy_set_header Host $http_host;\n");
            }

            host[serviceName].Config = serviceConfig.ToString();
        }

        /// <summary>
        /// Applies node description to configuration
        /// </summary>
        /// <param name="description">The node description</param>
        private void OnNewNodeDescription(WebDescriptionResponse description)
        {
            var nodeAddress = this.Sender.Path.Address;
            if (nodeAddress.Host == null)
            {
                // supposed this is local address
                nodeAddress = Cluster.Get(Context.System).SelfAddress;
            }

            if (!this.KnownActiveNodes.Contains(nodeAddress))
            {
                // node managed to go down before it was initialized
                return;
            }

            if (this.NodePublishUrls.ContainsKey(nodeAddress))
            {
                // duplicate configuration info
                return;
            }

            this.NodePublishUrls[nodeAddress] = description;

            foreach (var serviceDescription in description.Services)
            {
                var nodeServiceDescription = new NodeServiceConfiguration
                {
                    NodeAddress = nodeAddress,
                    ServiceDescription = serviceDescription
                };
                var serviceConfiguration = this.Configuration[serviceDescription.PublicHostName][serviceDescription.Route];
                if (!serviceConfiguration.ActiveNodes.Contains(nodeServiceDescription))
                {
                    serviceConfiguration.ActiveNodes.Add(nodeServiceDescription);
                }
            }

            this.WriteConfiguration();
        }

        /// <summary>
        /// Removes all references for node from configuration
        /// </summary>
        /// <param name="nodeAddress">The node address</param>
        private void OnWebNodeDown(Address nodeAddress)
        {
            this.KnownActiveNodes.Remove(nodeAddress);
            WebDescriptionResponse description;
            if (!this.NodePublishUrls.TryGetValue(nodeAddress, out description))
            {
                // something strange. Local data is corrupted;
                return;
            }

            foreach (var serviceHost in description.Services.GroupBy(s => s.PublicHostName))
            {
                var host = this.Configuration[serviceHost.Key];
                foreach (var serviceDescription in serviceHost)
                {
                    var activeNodes = host[serviceDescription.Route].ActiveNodes;
                    var d = activeNodes.FirstOrDefault(n => n.NodeAddress == nodeAddress);
                    if (d != null)
                    {
                        activeNodes.Remove(d);
                    }
                }

                host.Flush();
            }

            this.Configuration.Flush();
            this.NodePublishUrls.Remove(nodeAddress);
            this.WriteConfiguration();
        }

        /// <summary>
        /// Re-sends WebDescriptionRequest to any known Web nodes that haven't replied yet.
        /// </summary>
        private void RetryPending()
        {
            foreach (var address in this.KnownActiveNodes)
            {
                if (!this.NodePublishUrls.ContainsKey(address))
                {
                    Context.GetLogger().Info("{Type}: retrying WebDescriptionRequest to {Address}", this.GetType().Name, address);
                    Context.System.GetWebDescriptor(address).Tell(new WebDescriptionRequest(), this.Self);
                }
            }
        }

        /// <summary>
        /// Requests the node configuration for newly attached node
        /// </summary>
        /// <param name="nodeAddress">The node address</param>
        private void OnWebNodeUp(Address nodeAddress)
        {
            if (!this.KnownActiveNodes.Contains(nodeAddress))
            {
                this.KnownActiveNodes.Add(nodeAddress);
            }

            Context.System.GetWebDescriptor(nodeAddress)
                .Tell(new WebDescriptionRequest(), this.Self);
        }

        /// <summary>
        /// Writes current configuration to nginx config file and sends reload to nginx
        /// </summary>
        private void WriteConfiguration()
        {
            StringBuilder config = new StringBuilder();

            this.WriteUpStreamsToConfig(config);
            this.WriteServicesToConfig(config);
            Context.GetLogger().Info("{Type}: {NginxConfigContent}", this.GetType().Name, config.ToString());
            File.WriteAllText(this.configPath, config.ToString());

            if (this.reloadCommand != null)
            {
                var command = this.reloadCommand.GetString("Command");
                var arguments = this.reloadCommand.GetString("Arguments");
                if (command != null)
                {
                    var process = Process.Start(
                        new ProcessStartInfo(command, arguments)
                        {
                            UseShellExecute = false,
                            WorkingDirectory = Path.GetDirectoryName(command) ?? command
                        });

                    if (process != null && !process.WaitForExit(10000))
                    {
                        Context.GetLogger().Error("{Type}: NGinx reload command timeout", this.GetType().Name);
                    }
                }
            }
        }

        /// <summary>
        /// Writes every defined service to nginx config
        /// </summary>
        /// <param name="config">Configuration file to write</param>
        private void WriteServicesToConfig(StringBuilder config)
        {
            foreach (var host in this.Configuration)
            {
                config.Append("server {\n");
                config.Append(host.Config);
                foreach (var service in host)
                {
                    config.Append($"\tlocation {service.ServiceName} {{\n");
                    config.Append(service.Config);
                    if (service.ActiveNodes.Count > 0)
                    {
                        config.Append(
                            $"\t\tproxy_pass http://{this.GetUpStreamName(host.HostName, service.ServiceName)}{service.ServiceName};\n");
                        config.Append(
                            $"\t\t sub_filter {this.GetUpStreamName(host.HostName, service.ServiceName)} $host; \n");
                        config.Append("\t\t sub_filter_once off; \n");
                        config.Append("\t\t sub_filter_types text/xml; \n");
                        config.Append("\t\t sub_filter_types text/json; \n");
                        config.Append("\t\t sub_filter_types application/xml; \n");
                        config.Append("\t\t sub_filter_types application/json; \n");
                        config.Append("\t\t proxy_set_header OriginalHost $http_host;\n");
                        config.Append("\t\t proxy_set_header OriginalUri $request_uri;\n");
                    }

                    config.Append("\t}\n");
                }

                config.Append("}\n");
            }
        }

        /// <summary>
        /// Writes every defined upstream for every defined service to nginx config
        /// </summary>
        /// <param name="config">Configuration file to write</param>
        private void WriteUpStreamsToConfig(StringBuilder config)
        {
            foreach (var host in this.Configuration)
            {
                foreach (var service in host.Where(s => s.ActiveNodes.Count > 0))
                {
                    config.Append(
                        $@"
upstream {this.GetUpStreamName(host.HostName, service.ServiceName)} {{
    ip_hash;
{
                            string.Join("\n", service.ActiveNodes.Select(u => $"\tserver {u.NodeUrl};"))}
}}
");
                }
            }
        }
    }
}