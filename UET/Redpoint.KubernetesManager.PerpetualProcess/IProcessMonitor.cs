namespace Redpoint.KubernetesManager.PerpetualProcess
{
    public interface IProcessMonitor
    {
        Task<int> RunAsync(CancellationToken cancellationToken);
    }
}