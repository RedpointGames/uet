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
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultNodeProvisioningEndpointContext : INodeProvisioningEndpointContext
    {
        private readonly DirectoryInfo _storageDirectoryForAllNodes;

        public DefaultNodeProvisioningEndpointContext(
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

        public CancellationToken CancellationToken => HttpContext.RequestAborted;

        public IPAddress RemoteIpAddress => HttpContext.Connection.RemoteIpAddress;
    }
}
