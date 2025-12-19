namespace Redpoint.KubernetesManager.Services
{
    public interface IProcessMonitor
    {
        Task<int> RunAsync(CancellationToken cancellationToken);
    }
}