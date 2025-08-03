namespace Redpoint.KubernetesManager.Services
{
    using Redpoint.KubernetesManager.Models;
    using System.Net;
    using System.Threading.Tasks;

    public interface INodeManifestClient
    {
        Task<NodeManifest> ObtainNodeManifestAsync(IPAddress controllerAddress, string nodeName, CancellationToken stoppingToken);
    }
}
