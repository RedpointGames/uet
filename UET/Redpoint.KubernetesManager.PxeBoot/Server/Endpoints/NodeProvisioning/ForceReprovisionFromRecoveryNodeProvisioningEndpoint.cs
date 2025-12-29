namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Redpoint.KubernetesManager.PxeBoot.Api;
    using System.Net;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class ForceReprovisionFromRecoveryNodeProvisioningEndpoint : INodeProvisioningEndpoint
    {
        public string Path => "/force-reprovision";

        public bool RequireNodeObjects => true;

        public async Task HandleRequestAsync(INodeProvisioningEndpointContext context)
        {
            await context.ConfigurationSource.UpdateRkmNodeForceReprovisionByAttestationIdentityKeyFingerprintAsync(
                context.AikFingerprint,
                true,
                context.CancellationToken);

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                new ForceReprovisionNodeResponse(),
                ApiJsonSerializerContext.WithStringEnum.ForceReprovisionNodeResponse,
                context.CancellationToken);
        }
    }
}
