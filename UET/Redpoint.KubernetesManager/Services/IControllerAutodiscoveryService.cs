namespace Redpoint.KubernetesManager.Services
{
    using System.Threading.Tasks;

    internal interface IControllerAutodiscoveryService
    {
        Task<string?> AttemptAutodiscoveryOfController(CancellationToken stoppingToken);

        void StartAutodiscovery();
    }
}
