namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using k8s.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.Configuration.Types;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    internal class DefaultNodeProvisioningEndpointContext : INodeProvisioningEndpointContext
    {
        private readonly ILogger _logger;
        private readonly DirectoryInfo _storageDirectoryForAllNodes;

        public DefaultNodeProvisioningEndpointContext(
            ILogger logger,
            HttpContext httpContext,
            string aikPem,
            string aikFingerprint,
            IRkmConfigurationSource configurationSource,
            KubernetesRkmJsonSerializerContext jsonSerializerContext,
            DirectoryInfo storageDirectoryForAllNodes,
            string hostAddress,
            int hostHttpPort,
            int hostHttpsPort)
        {
            _logger = logger;
            HttpContext = httpContext;
            AikPem = aikPem;
            AikFingerprint = aikFingerprint;
            ConfigurationSource = configurationSource;
            JsonSerializerContext = jsonSerializerContext;
            _storageDirectoryForAllNodes = storageDirectoryForAllNodes;
            HostAddress = hostAddress;
            HostHttpPort = hostHttpPort;
            HostHttpsPort = hostHttpsPort;
        }

        public HttpContext HttpContext { get; }

        public string AikPem { get; }

        public string AikFingerprint { get; }

        public IRkmConfigurationSource ConfigurationSource { get; }

        public RkmNode? RkmNode { get; set; }

        public RkmNodeGroup? RkmNodeGroup { get; set; }

        public RkmNodeProvisioner? RkmNodeGroupProvisioner { get; set; }

        public RkmNodeProvisioner? RkmNodeProvisioner { get; set; }

        public KubernetesRkmJsonSerializerContext JsonSerializerContext { get; }

        public DirectoryInfo NodeFileStorageDirectory
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AikFingerprint))
                {
                    throw new InvalidOperationException();
                }

                return _storageDirectoryForAllNodes.CreateSubdirectory(AikFingerprint);
            }
        }

        public string HostAddress { get; }

        public int HostHttpPort { get; }

        public int HostHttpsPort { get; }

        public void MarkProvisioningCompleteForNode()
        {
            if (RkmNode?.Status?.Provisioner == null)
            {
                return;
            }

            RkmNode.Status.LastSuccessfulProvision = new RkmNodeStatusLastSuccessfulProvision
            {
                Name = RkmNode.Status.Provisioner.Name,
                Hash = RkmNode.Status.Provisioner.Hash,
            };
            RkmNode.Status.Provisioner = null;
        }

        public void UpdateRegisteredIpAddressesForNode()
        {
            if (RkmNode?.Status == null)
            {
                return;
            }

            var threshold = DateTimeOffset.UtcNow;
            var newExpiry = DateTimeOffset.UtcNow.AddDays(1);

            var addresses = new List<IPAddress>
            {
                HttpContext.Connection.RemoteIpAddress
            };
            if (HttpContext.Connection.RemoteIpAddress.IsIPv4MappedToIPv6)
            {
                addresses.Add(HttpContext.Connection.RemoteIpAddress.MapToIPv4());
            }

            RkmNode.Status.RegisteredIpAddresses ??= new List<RkmNodeStatusRegisteredIpAddress>();
            RkmNode.Status.RegisteredIpAddresses.RemoveAll(x => !x.ExpiresAt.HasValue || x.ExpiresAt.Value < threshold);

            foreach (var addressRaw in addresses)
            {
                var address = addressRaw.ToString();

                var existingEntry = RkmNode.Status.RegisteredIpAddresses.FirstOrDefault(x => x.Address == address);
                if (existingEntry != null)
                {
                    _logger.LogInformation($"Updating existing expiry of registered IP address '{address}' to {newExpiry}...");
                    existingEntry.ExpiresAt = DateTimeOffset.UtcNow.AddDays(1);
                }
                else
                {
                    _logger.LogInformation($"Adding new entry for registered IP address '{address}' with expiry {newExpiry}...");
                    RkmNode.Status.RegisteredIpAddresses.Add(new RkmNodeStatusRegisteredIpAddress
                    {
                        Address = address,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
                    });
                }
            }
        }
    }
}
