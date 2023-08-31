namespace Redpoint.AutoDiscovery
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Provides APIs for registering and discovering network services.
    /// </summary>
    public interface INetworkAutoDiscovery
    {
        /// <summary>
        /// Registers a service with DNS-SD until the resulting disposable is disposed.
        /// </summary>
        /// <param name="name">A service name, in the form <c>name._service._tcp.local</c>.</param>
        /// <param name="port">The port the service is being served on.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the initial registration request.</param>
        /// <returns></returns>
        Task<IAsyncDisposable> RegisterServiceAsync(
            string name,
            int port,
            CancellationToken cancellationToken);

        /// <summary>
        /// Streams network services discovered on the local network until the cancellation token is cancelled.
        /// The returned enumerable is infinite and only stops enumeration once the cancellation token is
        /// cancelled.
        /// </summary>
        /// <param name="name">The query, in the form <c>_service._tcp.local</c>.</param>
        /// <param name="cancellationToken">The cancellation token to stop discovery.</param>
        /// <returns>A stream of discovered network services.</returns>
        IAsyncEnumerable<NetworkService> DiscoverServicesAsync(
            string name,
            CancellationToken cancellationToken);
    }
}