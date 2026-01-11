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
            context.RkmNode!.Status ??= new();

            if (string.IsNullOrWhiteSpace(context.RkmNode.Status.BootEfiPath))
            {
                _logger.LogError("Can't schedule reboot to disk as 'setEfiBootPath' provisioning step hasn't been run, and no EFI image is bootable. Forcing reprovision...");
                context.RkmNode.Spec ??= new();
                context.RkmNode.Spec.ForceReprovision = true;

                await context.ConfigurationSource.CreateProvisioningEventForRkmNodeAsync(
                    context.AikFingerprint,
                    $"Force reprovisioning as a reboot to disk was scheduled with no EFI image set",
                    context.CancellationToken);

                await context.ConfigurationSource.UpdateRkmNodeForceReprovisionByAttestationIdentityKeyFingerprintAsync(
                    context.AikFingerprint,
                    true,
                    context.CancellationToken);
            }
            else
            {
                _logger.LogInformation($"Node {context.RkmNode!.Metadata.Name} is requesting to boot to disk on next PXE boot.");
                context.RkmNode.Status.BootToDisk = true;

                await context.ConfigurationSource.CreateProvisioningEventForRkmNodeAsync(
                    context.AikFingerprint,
                    $"Scheduling reboot to disk as provisioning is complete",
                    context.CancellationToken);
            }

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
