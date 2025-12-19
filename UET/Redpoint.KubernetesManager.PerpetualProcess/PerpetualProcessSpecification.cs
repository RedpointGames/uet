namespace Redpoint.KubernetesManager.PerpetualProcess
{
    using System.Diagnostics.CodeAnalysis;

    public class PerpetualProcessSpecification
    {
        public PerpetualProcessSpecification(
            string filename,
            string[] arguments,
            Dictionary<string, string>? environment = null,
            Func<CancellationToken, Task>? beforeStart = null,
            Func<CancellationToken, Task>? afterStart = null,
            bool wsl = false,
            bool silent = false)
        {
            Filename = filename;
            Arguments = arguments;
            Environment = environment;
            BeforeStart = beforeStart;
            AfterStart = afterStart;
            WSL = wsl;
            Silent = silent;
        }

        public string Filename { get; }

        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This is legacy code.")]
        public string[] Arguments { get; }

        public Dictionary<string, string>? Environment { get; }

        public Func<CancellationToken, Task>? BeforeStart { get; }

        public Func<CancellationToken, Task>? AfterStart { get; }

        public bool WSL { get; }

        public bool Silent { get; }
    }
}
