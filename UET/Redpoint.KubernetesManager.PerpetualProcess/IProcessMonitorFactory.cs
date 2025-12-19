namespace Redpoint.KubernetesManager.PerpetualProcess
{
    public interface IProcessMonitorFactory
    {
        [Obsolete("Use ProcessSpecification instead.")]
        IProcessMonitor CreatePerpetualProcess(string filename, string[] arguments, Dictionary<string, string>? environment, Func<CancellationToken, Task>? beforeStart = null, Func<CancellationToken, Task>? afterStart = null);

        IProcessMonitor CreatePerpetualProcess(PerpetualProcessSpecification processSpecification);

        [Obsolete("Use ProcessSpecification instead.")]
        IProcessMonitor CreateTerminatingProcess(string filename, string[] arguments, Dictionary<string, string>? environment = null, bool silent = false);

        IProcessMonitor CreateTerminatingProcess(PerpetualProcessSpecification processSpecification);
    }
}
