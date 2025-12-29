namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning
{
    using System.Threading.Tasks;

    internal interface INodeProvisioningEndpoint
    {
        string Path { get; }

        bool RequireNodeObjects { get; }

        Task HandleRequestAsync(INodeProvisioningEndpointContext context);
    }
}
