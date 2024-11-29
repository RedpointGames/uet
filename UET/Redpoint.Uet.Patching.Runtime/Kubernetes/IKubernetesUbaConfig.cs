namespace Redpoint.Uet.Patching.Runtime.Kubernetes
{
    internal interface IKubernetesUbaConfig
    {
        string? Namespace { get; }
        string? Context { get; }
        string? SmbServer { get; }
        string? SmbShare { get; }
        string? SmbUsername { get; }
        string? SmbPassword { get; }

        public const ulong MemoryBytesPerCore = 1610612736uL;
    }
}
