namespace Redpoint.OpenGE.PreprocessorCache
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using PreprocessorCacheApi;
    using Redpoint.GrpcPipes;
    using Redpoint.ProcessExecution;
    using System.Threading;
    using System.Threading.Tasks;

    internal class SystemWidePreprocessorCache : IPreprocessorCache, IDisposable
    {
        private readonly ILogger<SystemWidePreprocessorCache> _logger;
        private readonly IGrpcPipeFactory _grpcPipeFactory;
        private readonly IProcessExecutor _processExecutor;
        private readonly ProcessSpecification _daemonLaunchSpecification;
        private readonly SemaphoreSlim _clientCreatingSemaphore;
        private readonly CancellationTokenSource _daemonCancellationTokenSource;
        private PreprocessorCache.PreprocessorCacheClient? _currentClient;
        private Task<int>? _daemonProcess;

        public SystemWidePreprocessorCache(
            ILogger<SystemWidePreprocessorCache> logger,
            IGrpcPipeFactory grpcPipeFactory,
            IProcessExecutor processExecutor,
            ProcessSpecification daemonLaunchSpecification)
        {
            _logger = logger;
            _grpcPipeFactory = grpcPipeFactory;
            _processExecutor = processExecutor;
            _daemonLaunchSpecification = daemonLaunchSpecification;
            _clientCreatingSemaphore = new SemaphoreSlim(1);
            _daemonCancellationTokenSource = new CancellationTokenSource();
            _currentClient = null;
            _daemonProcess = null;
        }

        private async Task<PreprocessorCache.PreprocessorCacheClient> GetClientAsync(bool spawn = false)
        {
            await _clientCreatingSemaphore.WaitAsync();
            try
            {
                if (spawn && (_daemonProcess == null || _daemonProcess.IsCompleted))
                {
                    _daemonProcess = Task.Run(async () => await _processExecutor.ExecuteAsync(
                        _daemonLaunchSpecification,
                        CaptureSpecification.Passthrough,
                        _daemonCancellationTokenSource.Token));
                }
                // @note: Do not re-use current client if we were just told to spawn daemon.
                else if (!spawn && _currentClient != null)
                {
                    return _currentClient;
                }

                if (spawn)
                {
                    // @note: Pace the rate at which we re-create the client if we're trying to spawn the daemon.
                    await Task.Delay(10);
                }

                _currentClient = _grpcPipeFactory.CreateClient(
                    "OpenGEPreprocessorCache",
                    GrpcPipeNamespace.Computer,
                    channel => new PreprocessorCache.PreprocessorCacheClient(channel));
                return _currentClient;
            }
            finally
            {
                _clientCreatingSemaphore.Release();
            }
        }

        public async Task EnsureConnectedAsync()
        {
            var client = await GetClientAsync();
            do
            {
                try
                {
                    await client.PingAsync(new PingRequest());
                    return;
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
                {
                    client = await GetClientAsync(true);
                    continue;
                }
            } while (true);
        }

        public async Task<PreprocessorScanResultWithCacheMetadata> GetUnresolvedDependenciesAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            var client = await GetClientAsync();
            do
            {
                try
                {
                    return (await client.GetUnresolvedDependenciesAsync(
                        new GetUnresolvedDependenciesRequest
                        {
                            Path = filePath,
                        },
                        cancellationToken: cancellationToken)).Result;
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
                {
                    client = await GetClientAsync(true);
                    continue;
                }
            } while (true);
        }

        public async void Dispose()
        {
            _daemonCancellationTokenSource.Cancel();
            if (_daemonProcess != null)
            {
                try
                {
                    await _daemonProcess;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }
}
