namespace Redpoint.AutoDiscovery
{
    public interface INetworkAutoDiscovery
    {
        Task<IAsyncDisposable> RegisterServiceAsync(
            string name,
            int port,
            CancellationToken cancellationToken);

        IAsyncEnumerable<NetworkService> DiscoverServicesAsync(string name);
    }
}