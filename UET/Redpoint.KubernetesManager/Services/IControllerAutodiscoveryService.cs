namespace Redpoint.KubernetesManager.Services
{
    using System.Threading.Tasks;

    public interface IControllerAutodiscoveryService
    {
        Task<string?> AttemptAutodiscoveryOfController(CancellationToken stoppingToken);

        void StartAutodiscovery();
    }
}
