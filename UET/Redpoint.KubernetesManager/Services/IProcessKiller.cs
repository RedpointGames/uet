namespace Redpoint.KubernetesManager.Services
{
    public interface IProcessKiller
    {
        Task EnsureProcessesAreNotRunning(string[] processNames, CancellationToken cancellationToken);
    }
}
