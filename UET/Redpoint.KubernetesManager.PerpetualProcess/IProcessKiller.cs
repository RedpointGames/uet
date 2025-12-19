namespace Redpoint.KubernetesManager.PerpetualProcess
{
    public interface IProcessKiller
    {
        Task EnsureProcessesAreNotRunning(string[] processNames, CancellationToken cancellationToken);
    }
}
