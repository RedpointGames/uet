namespace Redpoint.KubernetesManager.Services
{
    using Redpoint.KubernetesManager.Models;

    internal interface IProcessMonitorFactory
    {
        internal interface IProcessMonitor
        {
            Task<int> RunAsync(CancellationToken cancellationToken);
        }

        [Obsolete("Use ProcessSpecification instead.")]
        IProcessMonitor CreatePerpetualProcess(string filename, string[] arguments, Dictionary<string, string>? environment, Func<CancellationToken, Task>? beforeStart = null, Func<CancellationToken, Task>? afterStart = null);

        IProcessMonitor CreatePerpetualProcess(ProcessSpecification processSpecification);

        [Obsolete("Use ProcessSpecification instead.")]
        IProcessMonitor CreateTerminatingProcess(string filename, string[] arguments, Dictionary<string, string>? environment = null, bool silent = false);

        IProcessMonitor CreateTerminatingProcess(ProcessSpecification processSpecification);
    }

}
