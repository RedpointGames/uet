namespace Redpoint.KubernetesManager.Services
{
    internal interface IProcessKiller
    {
        Task EnsureProcessesAreNotRunning(CancellationToken cancellationToken);
    }
}
