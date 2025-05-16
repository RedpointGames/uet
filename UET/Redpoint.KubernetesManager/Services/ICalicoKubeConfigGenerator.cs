namespace Redpoint.KubernetesManager.Services.Windows
{
    using k8s;
    using System.Runtime.Versioning;
    using System.Threading.Tasks;

    public interface ICalicoKubeConfigGenerator
    {
        Task<string> ProvisionCalicoKubeConfigIfNeededAsync(IKubernetes kubernetes, CancellationToken stoppingToken);
    }
}
