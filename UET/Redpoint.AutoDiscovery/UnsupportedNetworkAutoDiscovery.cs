namespace Redpoint.AutoDiscovery
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class UnsupportedNetworkAutoDiscovery : INetworkAutoDiscovery
    {
        private readonly ILogger<UnsupportedNetworkAutoDiscovery> _logger;

        public UnsupportedNetworkAutoDiscovery(
            ILogger<UnsupportedNetworkAutoDiscovery> logger)
        {
            _logger = logger;
        }

        public IAsyncEnumerable<NetworkService> DiscoverServicesAsync(string query, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Network auto-discovery is not supported on this operating system.");
            return Array.Empty<NetworkService>().ToAsyncEnumerable();
        }

        public Task<IAsyncDisposable> RegisterServiceAsync(string name, int port, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Network auto-discovery is not supported on this operating system.");
            return Task.FromResult<IAsyncDisposable>(new NullDisposable());
        }

        private sealed class NullDisposable : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
