namespace Redpoint.AutoDiscovery
{
    extern alias SDWin64;

    using Redpoint.AutoDiscovery.Windows;
    using Redpoint.Concurrency;
    using System.Runtime.CompilerServices;
    using System.Runtime.Versioning;
    using System.Threading;

    [SupportedOSPlatform("windows10.0.10240")]
    internal class Win64NetworkAutoDiscovery : INetworkAutoDiscovery
    {
        private class DnsDeregisterAsyncDisposable : IAsyncDisposable
        {
            private readonly Win64ServiceInstance _serviceInstance;

            public DnsDeregisterAsyncDisposable(Win64ServiceInstance serviceInstance)
            {
                _serviceInstance = serviceInstance;
            }

            public async ValueTask DisposeAsync()
            {
                var request = new Win64ServiceDeRegisterCall(_serviceInstance);
                await request.ExecuteAsync(CancellationToken.None);
            }
        }

        public async Task<IAsyncDisposable> RegisterServiceAsync(
            string name,
            int port,
            CancellationToken cancellationToken)
        {
            var request = new Win64ServiceRegisterCall(name, (ushort)port);
            var nativeRequest = await request.ExecuteAsync(cancellationToken);
            var serviceInstance = (Win64ServiceInstance)nativeRequest.DisposablePtrs[0];
            return new DnsDeregisterAsyncDisposable(serviceInstance);
        }

        public async IAsyncEnumerable<NetworkService> DiscoverServicesAsync(
            string query,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var stream = new TerminableAwaitableConcurrentQueue<NetworkService>();
            var request = new Win64ServiceBrowseCall(query, stream);
            var task = Task.Run(async () =>
            {
                try
                {
                    await request.ExecuteAsync(cancellationToken);
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
                {
                    // Consume this OperationCanceledException.
                }
                finally
                {
                    stream.Terminate();
                }
            });
            await foreach (var entry in stream)
            {
                yield return entry;
            }
            await task;
        }
    }
}