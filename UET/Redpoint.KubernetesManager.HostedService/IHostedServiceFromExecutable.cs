namespace Redpoint.KubernetesManager.HostedService
{
    using System.Threading.Tasks;

    public interface IHostedServiceFromExecutable
    {
        Task RunHostedServicesAsync(CancellationToken cancellationToken);
    }
}
