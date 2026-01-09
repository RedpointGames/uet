namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using Microsoft.AspNetCore.Http;
    using System.Net;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class QueryServicesNodeProvisioningEndpoint : INodeProvisioningEndpoint
    {
        public string Path => "/query-services";

        public bool RequireNodeObjects => true;

        public async Task HandleRequestAsync(INodeProvisioningEndpointContext context)
        {
            var services = context.RkmNodeGroup?.Spec?.Services ?? new();

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    services,
                    context.JsonSerializerContext.RkmNodeGroupSpecServices),
                context.CancellationToken);
        }
    }
}
