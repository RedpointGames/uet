namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    internal interface IProvisionContextDiscoverer
    {
        Task<ProvisionContext> GetProvisionContextAsync(
            bool isLocal,
            CancellationToken cancellationToken);
    }
}
