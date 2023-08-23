namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Google.Protobuf;
    using Grpc.Core;
    using Grpc.Net.Client;
    using Grpc.Net.Client.Balancer;
    using Grpc.Net.Client.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.AutoDiscovery;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultWorkerPool : IWorkerPool
    {
        private readonly ILogger<DefaultWorkerPool> _logger;
        private readonly INetworkAutoDiscovery _networkAutoDiscovery;
        private readonly SemaphoreSlim _notifyReevaluationOfWorkers;
        private readonly CancellationTokenSource _disposedCts;
        internal readonly WorkerSubpool _localSubpool;
        internal readonly WorkerSubpool _remoteSubpool;
        internal readonly ConcurrentDictionary<string, bool> _remoteWorkersHandled;
        private readonly Task _workersProcessingTask;
        private readonly Task _discoverRemoteWorkersTask;
        private readonly string? _localWorkerUniqueId;

        public DefaultWorkerPool(
            ILogger<DefaultWorkerPool> logger,
            ILogger<WorkerSubpool> subpoolLogger,
            INetworkAutoDiscovery networkAutoDiscovery,
            WorkerAddRequest? localWorkerAddRequest)
        {
            _logger = logger;
            _networkAutoDiscovery = networkAutoDiscovery;
            _notifyReevaluationOfWorkers = new SemaphoreSlim(0);
            _disposedCts = new CancellationTokenSource();
            _localSubpool = new WorkerSubpool(
                subpoolLogger,
                _notifyReevaluationOfWorkers);
            _remoteSubpool = new WorkerSubpool(
                subpoolLogger,
                _notifyReevaluationOfWorkers);
            _remoteWorkersHandled = new ConcurrentDictionary<string, bool>();

            if (localWorkerAddRequest != null)
            {
                _localWorkerUniqueId = localWorkerAddRequest.UniqueId;
                _localSubpool._workers.Add(new WorkerState
                {
                    DisplayName = localWorkerAddRequest.DisplayName,
                    Client = localWorkerAddRequest.Client,
                    UniqueId = localWorkerAddRequest.UniqueId,
                    IsLocalWorker = true,
                });
                _remoteSubpool._workers.Add(new WorkerState
                {
                    DisplayName = localWorkerAddRequest.DisplayName,
                    Client = localWorkerAddRequest.Client,
                    UniqueId = localWorkerAddRequest.UniqueId,
                    IsLocalWorker = true,
                });
            }

            _workersProcessingTask = Task.Run(PeriodicallyProcessWorkers);
            _discoverRemoteWorkersTask = Task.Run(DiscoverRemoteWorkers);
        }

        private async Task DiscoverRemoteWorkers()
        {
            while (true)
            {
                try
                {
                    await foreach (var entry in _networkAutoDiscovery.DiscoverServicesAsync(
                        $"_{WorkerDiscoveryConstants.OpenGEPlatformIdentifier}{WorkerDiscoveryConstants.OpenGEProtocolVersion}-openge._tcp.local",
                        _disposedCts.Token))
                    {
                        var workerUniqueId = entry.ServiceName.Split('.')[0];
                        if (_localWorkerUniqueId == workerUniqueId)
                        {
                            // This is our local worker, which we're already connected to directly.
                            continue;
                        }

                        if (_remoteSubpool.HasWorker(workerUniqueId))
                        {
                            // We already have this worker. Do not connect to it again.
                            continue;
                        }

                        var factory = new StaticResolverFactory(addr => entry.TargetAddressList.Select(x => new BalancerAddress(x.ToString(), entry.TargetPort)));
                        var services = new ServiceCollection();
                        services.AddSingleton<ResolverFactory>(factory);

                        // Try to ping this remote worker.
                        var taskApi = new TaskApi.TaskApiClient(
                            GrpcChannel.ForAddress(
                                $"static:///{entry.ServiceName}",
                                new GrpcChannelOptions
                                {
                                    ServiceConfig = new ServiceConfig
                                    {
                                        LoadBalancingConfigs =
                                        {
                                            new PickFirstConfig(),
                                        }
                                    },
                                    ServiceProvider = services.BuildServiceProvider(),
                                    Credentials = ChannelCredentials.Insecure,
                                }));
                        var usable = false;
                        try
                        {
                            await taskApi.PingTaskServiceAsync(
                                new PingTaskServiceRequest(),
                                deadline: DateTime.UtcNow.AddSeconds(5));
                            usable = true;
                        }
                        catch
                        {
                        }
                        if (usable)
                        {
                            await _remoteSubpool.RegisterWorkerAsync(new WorkerAddRequest
                            {
                                DisplayName = entry.TargetHostname,
                                UniqueId = workerUniqueId,
                                Client = taskApi,
                            });
                            _logger.LogInformation($"Discovered remote worker {entry.TargetHostname} at '{entry.ServiceName}'.");
                        }
                        else
                        {
                            _logger.LogWarning($"Discovered remote worker at {entry.ServiceName}, but it wasn't usable based on the gRPC ping request.");
                        }
                    }
                }
                catch (OperationCanceledException) when (_disposedCts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, $"Critical failure in worker discovery loop: {ex.Message}");
                    await Task.Delay(5000);
                }
            }
        }

        private async Task PeriodicallyProcessWorkers()
        {
            while (!_disposedCts.IsCancellationRequested)
            {
                // Reprocess remote workers state either:
                // - Every 10 seconds, or
                // - When the notification semaphore tells us we need to reprocess now.
                var timingCts = CancellationTokenSource.CreateLinkedTokenSource(
                    new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token,
                    _disposedCts.Token);
                try
                {
                    await _notifyReevaluationOfWorkers.WaitAsync(timingCts.Token);
                }
                catch (OperationCanceledException) when (timingCts.IsCancellationRequested)
                {
                }
                if (_disposedCts.IsCancellationRequested)
                {
                    // The worker pool is disposing.
                    return;
                }

                await _localSubpool.ProcessWorkersAsync();
                await _remoteSubpool.ProcessWorkersAsync();
            }
        }

        public Task<IWorkerCore> ReserveCoreAsync(
            bool requireLocalCore,
            CancellationToken cancellationToken)
        {
            if (requireLocalCore)
            {
                return _localSubpool.ReserveCoreAsync(cancellationToken);
            }
            else
            {
                return _remoteSubpool.ReserveCoreAsync(cancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _disposedCts.Cancel();
            try
            {
                await _workersProcessingTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
