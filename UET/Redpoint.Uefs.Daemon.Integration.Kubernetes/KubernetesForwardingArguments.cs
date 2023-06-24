namespace Redpoint.Uefs.Daemon.Integration.Kubernetes
{
    internal class KubernetesForwardingArguments
    {
        public int Port { get; set; }

        public string? UnixSocketPath { get; set; }
    }
}
