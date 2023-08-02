namespace Redpoint.OpenGE.Component.PreprocessorCache
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.GrpcPipes;
    using Redpoint.ProcessExecution;
    using System.Threading;
    using System.Threading.Tasks;

    internal class OnDemandClientPreprocessorCache : IPreprocessorCache, IDisposable
    {
        private readonly ILogger<OnDemandClientPreprocessorCache> _logger;
        private readonly IGrpcPipeFactory _grpcPipeFactory;
        private readonly IProcessExecutor _processExecutor;
        private readonly ProcessSpecification _daemonLaunchSpecification;
        private readonly SemaphoreSlim _clientCreatingSemaphore;
        private readonly CancellationTokenSource _daemonCancellationTokenSource;
        private PreprocessorCacheApi.PreprocessorCacheApiClient? _currentClient;
        private Task<int>? _daemonProcess;

        public OnDemandClientPreprocessorCache(
            ILogger<OnDemandClientPreprocessorCache> logger,
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

        private async Task<PreprocessorCacheApi.PreprocessorCacheApiClient> GetClientAsync(bool spawn = false)
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
                    channel => new PreprocessorCacheApi.PreprocessorCacheApiClient(channel));
                return _currentClient;
            }
            finally
            {
                _clientCreatingSemaphore.Release();
            }
        }

        public async Task EnsureAsync()
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

        public async Task<PreprocessorResolutionResultWithTimingMetadata> GetResolvedDependenciesAsync(
            string filePath,
            string[] forceIncludesFromPch,
            string[] forceIncludes,
            string[] includeDirectories,
            string[] systemDirectories,
            Dictionary<string, string> globalDefinitions,
            CancellationToken cancellationToken)
        {
            var client = await GetClientAsync();
            do
            {
                try
                {
                    var request = new GetResolvedDependenciesRequest
                    {
                        Path = filePath,
                    };
                    request.IncludeDirectories.AddRange(includeDirectories);
                    request.SystemIncludeDirectories.AddRange(systemDirectories);
                    request.GlobalDefinitions.Add(globalDefinitions);
                    request.ForceIncludePaths.AddRange(forceIncludes);
                    request.ForceIncludeFromPchPaths.AddRange(forceIncludesFromPch);

                    return (await client.GetResolvedDependenciesAsync(request, cancellationToken: cancellationToken)).Result;
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
