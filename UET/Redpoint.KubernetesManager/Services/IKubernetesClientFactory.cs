namespace Redpoint.KubernetesManager.Services
{
    using k8s;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IKubernetesClientFactory
    {
        Task<IKubernetes?> ConnectToClusterAsync(string configFile, int maximumWaitSeconds, CancellationToken cancellationToken);
    }
}