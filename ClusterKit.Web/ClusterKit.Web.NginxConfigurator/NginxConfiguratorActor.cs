﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NginxConfiguratorActor.cs" company="ClusterKit">
//   All rights reserved
// </copyright>
// <summary>
//   Follows cluster changes for adding / removing new nodes with "web" role and configures local nginx for supported urls
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ClusterKit.Web.NginxConfigurator
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

    using ClusterKit.Web.Client;
    using ClusterKit.Web.Client.Messages;

    using JetBrains.Annotations;

    using Serilog;

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
        public NginxConfiguratorActor()
        {
            this.configPath = Context.System.Settings.Config.GetString("ClusterKit.Web.Nginx.PathToConfig");
            this.reloadCommand = Context.System.Settings.Config.GetConfig("ClusterKit.Web.Nginx.ReloadCommand");
            this.InitFromConfiguration();

            Cluster.Get(Context.System)
                .Subscribe(
                    this.Self,
                    ClusterEvent.InitialStateAsEvents,
                    new[] { typeof(ClusterEvent.IMemberEvent), typeof(ClusterEvent.RoleLeaderChanged) });

            this.Receive<ClusterEvent.MemberUp>(
                m => m.Member.Roles.Contains("Web"),
                m => this.OnWebNodeUp(m.Member.Address));

            this.Receive<ClusterEvent.MemberRemoved>(
                m => m.Member.Roles.Contains("Web"),
                m => this.OnWebNodeDown(m.Member.Address));

            this.Receive<WebDescriptionResponse>(r => this.OnNewNodeDescription(r));
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
        /// Gets cahed data of published web urls in everey known node
        /// </summary>
        public Dictionary<Address, string> NodePublishUrls { get; } = new Dictionary<Address, string>();

        /// <summary>
        /// Compiles upstream name from hostname and servicename
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

            return $"ClusterKitWeb_{hostName.Replace('.', '_')}_{serviceName.Replace('/', '_').Replace('.', '_')}";
        }

        /// <summary>
        /// Initialized base nginx configuration from self configuration
        /// </summary>
        private void InitFromConfiguration()
        {
            var config = Context.System.Settings.Config.GetConfig("ClusterKit.Web.Nginx.Configuration");
            if (config == null)
            {
                return;
            }

            foreach (var pair in config.AsEnumerable())
            {
                var hostName = pair.Key;
                this.InitHostFromConfiguration(hostName, config.GetConfig(hostName));
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
                    && parameter.Key.StartsWith("location ", StringComparison.InvariantCultureIgnoreCase))
                {
                    var serviceName = parameter.Key.Substring("location ".Length).Trim();
                    this.InitServiceFromConfiguration(
                        this.Configuration[hostName],
                        serviceName,
                        config.GetConfig(parameter.Key));
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
                    foreach (var hoconValue in parameter.Value.GetArray())
                    {
                        serviceConfig.AppendFormat("\t\t{0} {1};\n", parameter.Key, hoconValue.GetString());
                    }
                }
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

            var nodeUrl = $"{nodeAddress.Host}:{description.ListeningPort}";
            this.NodePublishUrls[nodeAddress] = nodeUrl;

            foreach (var serviceDescription in description.ServiceNames)
            {
                var serviceConfiguration = this.Configuration[serviceDescription.Value][serviceDescription.Key];
                if (!serviceConfiguration.ActiveNodes.Contains(nodeUrl))
                {
                    serviceConfiguration.ActiveNodes.Add(nodeUrl);
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
            string nodeUrl;
            if (!this.NodePublishUrls.TryGetValue(nodeAddress, out nodeUrl))
            {
                // something sttrange. Local data is corrupted;
                return;
            }

            foreach (var host in this.Configuration)
            {
                foreach (var service in host)
                {
                    service.ActiveNodes.Remove(nodeUrl);
                }

                host.Flush();
            }

            this.Configuration.Flush();
            this.NodePublishUrls.Remove(nodeAddress);
            this.WriteConfiguration();
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
                    var proccess = Process.Start(
                        new ProcessStartInfo(command, arguments)
                        {
                            UseShellExecute = false,
                            WorkingDirectory = Path.GetDirectoryName(command) ?? ".",
                        });

                    if (proccess != null && !proccess.WaitForExit(10000))
                    {
                        Context.GetLogger().Error("{Type}: NGinx reload command timeou", this.GetType().Name);
                    }
                }
            }
        }

        /// <summary>
        /// Writes every defined service to nginx config
        /// </summary>
        /// <param name="config">Configuration file to write</param>
        ///
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
                    }

                    config.Append("\t}\n");

                    // swagger
                    if (service.ActiveNodes.Count > 0)
                    {
                        config.Append($"\tlocation {service.ServiceName}/swagger {{\n");
                        config.Append($"\t\trewrite {service.ServiceName}/swagger(.*)$ /swagger$1 break;\n");
                        config.Append(
                            $"\t\tproxy_pass http://{this.GetUpStreamName(host.HostName, service.ServiceName)};\n");
                        config.Append("\t\tproxy_set_header Host $host;\n ");
                        config.Append("\t}\n");
                    }
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
                            string.Join("\n", service.ActiveNodes.Select(u => $"\tserver {u};"))}
}}
");
                }
            }
        }
    }
}