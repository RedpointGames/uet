namespace Redpoint.KubernetesManager.Services
{
    public interface IProcessKiller
    {
        Task EnsureProcessesAreNotRunning(CancellationToken cancellationToken);

        Task EnsureProcessesAreNotRunning(string[] processNames, CancellationToken cancellationToken);
    }
}
