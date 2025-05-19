namespace Redpoint.KubernetesManager.Services
{
    using System.Threading.Tasks;

    internal interface INetworkingConfiguration
    {
        Task<bool> ConfigureForKubernetesAsync(bool isController, CancellationToken stoppingToken);
    }
}
