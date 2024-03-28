namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.Logging;
    using Redpoint.AutoDiscovery;
    using Redpoint.Concurrency;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;
    using Redpoint.Tasks;
    using Redpoint.GrpcPipes;
    using System.Net;

    internal class DefaultTaskApiWorkerPool : ITaskApiWorkerPool
    {
        private readonly ILogger<DefaultTaskApiWorkerPool> _logger;
        private readonly INetworkAutoDiscovery _networkAutoDiscovery;
        private readonly ITaskScheduler _taskScheduler;
        private readonly IGrpcPipeFactory _grpcPipeFactory;
        internal readonly WorkerCoreRequestCollection<ITaskApiWorkerCore> _requestCollection;
        private readonly Mutex _disposing;
        private bool _disposed;
        private readonly ITaskSchedulerScope _taskSchedulerScope;
        private readonly string? _localWorkerUniqueId;
        private readonly TaskApiWorkerCoreProvider? _localWorkerCoreProvider;
        internal readonly SingleSourceWorkerCoreRequestFulfiller<ITaskApiWorkerCore>? _localWorkerFulfiller;
        private readonly WorkerCoreProviderCollection<ITaskApiWorkerCore>? _remoteWorkerCoreProviderCollection;
        internal readonly MultipleSourceWorkerCoreRequestFulfiller<ITaskApiWorkerCore>? _remoteWorkerFulfiller;
        private readonly Task? _remoteWorkerDiscoveryTask;

        public DefaultTaskApiWorkerPool(
            ILogger<DefaultTaskApiWorkerPool> logger,
            INetworkAutoDiscovery networkAutoDiscovery,
            ITaskScheduler taskScheduler,
            IGrpcPipeFactory grpcPipeFactory,
            TaskApiWorkerPoolConfiguration poolConfiguration)
        {
            _logger = logger;
            _networkAutoDiscovery = networkAutoDiscovery;
            _taskScheduler = taskScheduler;
            _grpcPipeFactory = grpcPipeFactory;
            _requestCollection = new WorkerCoreRequestCollection<ITaskApiWorkerCore>();
            _disposing = new Mutex();
            _disposed = false;
            _taskSchedulerScope = taskScheduler.CreateSchedulerScope("TaskApiWorkerPool", CancellationToken.None);

            if (poolConfiguration.LocalWorker != null)
            {
                _localWorkerUniqueId = poolConfiguration.LocalWorker.UniqueId;
                _localWorkerCoreProvider = new TaskApiWorkerCoreProvider(
                    _logger,
                    taskScheduler,
                    poolConfiguration.LocalWorker.Client,
                    poolConfiguration.LocalWorker.UniqueId,
                    poolConfiguration.LocalWorker.DisplayName);
                _localWorkerFulfiller = new SingleSourceWorkerCoreRequestFulfiller<ITaskApiWorkerCore>(
                    _logger,
                    taskScheduler,
                    _requestCollection,
                    _localWorkerCoreProvider,
                    true,
                    100);
            }

            if (poolConfiguration.EnableNetworkAutoDiscovery)
            {
                _remoteWorkerCoreProviderCollection = new WorkerCoreProviderCollection<ITaskApiWorkerCore>();
                _remoteWorkerFulfiller = new MultipleSourceWorkerCoreRequestFulfiller<ITaskApiWorkerCore>(
                    _logger,
                    taskScheduler,
                    _requestCollection,
                    _remoteWorkerCoreProviderCollection,
                    false);
                _remoteWorkerDiscoveryTask = _taskSchedulerScope.RunAsync("DiscoverRemoteWorkers", DiscoverRemoteWorkersAsync, CancellationToken.None);
            }
        }

        public void SetTracer(WorkerPoolTracer tracer)
        {
            _localWorkerFulfiller?.SetTracer(tracer);
            _remoteWorkerFulfiller?.SetTracer(tracer);
        }

        private async Task DiscoverRemoteWorkersAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await foreach (var entry in _networkAutoDiscovery.DiscoverServicesAsync(
                        $"_{WorkerDiscoveryConstants.OpenGEPlatformIdentifier}{WorkerDiscoveryConstants.OpenGEProtocolVersion}-openge._tcp.local",
                        cancellationToken))
                    {
                        var workerUniqueId = entry.ServiceName.Split('.')[0];
                        if (_localWorkerUniqueId == workerUniqueId)
                        {
                            // This is our local worker, which we're already connected to directly.
                            continue;
                        }

                        if (await _remoteWorkerCoreProviderCollection!.HasAsync(workerUniqueId).ConfigureAwait(false))
                        {
                            // We already have this worker. Do not connect to it again.
                            continue;
                        }

                        TaskApi.TaskApiClient? taskApi = null;
                        foreach (var address in entry.TargetAddressList)
                        {
                            var usable = false;
                            try
                            {
                                var attemptedTaskApi = _grpcPipeFactory.CreateNetworkClient(
                                    new IPEndPoint(address, entry.TargetPort),
                                    x => new TaskApi.TaskApiClient(x));
                                await attemptedTaskApi.PingTaskServiceAsync(new PingTaskServiceRequest(), deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);
                                usable = true;
                                taskApi = attemptedTaskApi;
                            }
                            catch
                            {
                            }
                            if (usable)
                            {
                                break;
                            }
                        }
                        if (taskApi != null)
                        {
                            var newProvider = new TaskApiWorkerCoreProvider(
                                _logger,
                                _taskScheduler,
                                taskApi,
                                workerUniqueId,
                                entry.TargetHostname);
                            await newProvider.OnTaskApiDisconnected.AddAsync(OnRemoteWorkerDisconnectedAsync).ConfigureAwait(false);
                            await _remoteWorkerCoreProviderCollection.AddAsync(newProvider).ConfigureAwait(false);
                            _logger.LogInformation($"Discovered remote worker {entry.TargetHostname} at '{entry.ServiceName}'.");
                        }
                        else
                        {
                            _logger.LogWarning($"Discovered remote worker at {entry.ServiceName}, but it wasn't usable based on the gRPC ping request. The addresses we tried were: {string.Join(", ", entry.TargetAddressList.Select(x => x.ToString()))}");
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, $"Critical failure in worker discovery loop: {ex.Message}");
                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task OnRemoteWorkerDisconnectedAsync(
            IWorkerCoreProvider<ITaskApiWorkerCore> provider,
            CancellationToken token)
        {
            var castedProvider = (TaskApiWorkerCoreProvider)provider;
            _logger.LogTrace($"Notified that remote worker {castedProvider.DisplayName} is going away.");
            await _remoteWorkerCoreProviderCollection!.RemoveAsync(provider).ConfigureAwait(false);
        }

        public Task<IWorkerCoreRequest<ITaskApiWorkerCore>> ReserveCoreAsync(
            CoreAllocationPreference corePreference,
            CancellationToken cancellationToken)
        {
            return _requestCollection.CreateFulfilledRequestAsync(
                corePreference,
                cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            using var _ = await _disposing.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            _logger.LogTrace("Worker pool is now shutting down.");

            if (!_disposed)
            {
                if (_localWorkerFulfiller != null)
                {
                    _logger.LogTrace("Waiting for local worker request fulfiller to dispose...");
                    await _localWorkerFulfiller.DisposeAsync().ConfigureAwait(false);
                }
                if (_remoteWorkerFulfiller != null)
                {
                    _logger.LogTrace("Waiting for remote worker request fulfiller to dispose...");
                    await _remoteWorkerFulfiller.DisposeAsync().ConfigureAwait(false);
                }
                if (_remoteWorkerDiscoveryTask != null)
                {
                    try
                    {
                        _logger.LogTrace("Waiting for remote worker discovery task to complete...");
                        await _taskSchedulerScope.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
                _disposed = true;
            }
        }
    }
}
