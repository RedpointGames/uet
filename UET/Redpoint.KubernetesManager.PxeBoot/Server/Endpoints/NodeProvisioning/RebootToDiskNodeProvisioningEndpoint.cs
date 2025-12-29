namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    internal class RebootToDiskNodeProvisioningEndpoint : INodeProvisioningEndpoint
    {
        private readonly ILogger<RebootToDiskNodeProvisioningEndpoint> _logger;

        public RebootToDiskNodeProvisioningEndpoint(
            ILogger<RebootToDiskNodeProvisioningEndpoint> logger)
        {
            _logger = logger;
        }

        public string Path => "/reboot-to-disk";

        public bool RequireNodeObjects => true;

        public async Task HandleRequestAsync(INodeProvisioningEndpointContext context)
        {
            _logger.LogInformation($"Node {context.RkmNode!.Metadata.Name} is requesting to boot to disk on next PXE boot.");

            context.RkmNode.Status ??= new();
            context.RkmNode.Status.BootToDisk = true;
            context.UpdateRegisteredIpAddressesForNode();

            await context.ConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                context.AikFingerprint,
                context.RkmNode.Status,
                context.CancellationToken);

            context.Response.StatusCode = 200;
            return;
        }
    }
}
