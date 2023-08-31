namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Grpc.Core;
    using Grpc.Net.Client.Balancer;
    using Grpc.Net.Client.Configuration;
    using Grpc.Net.Client;
    using Microsoft.AspNetCore.Connections.Features;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.AutoDiscovery;
    using Redpoint.Concurrency;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;
    using static Grpc.Core.Metadata;

    internal class DefaultTaskApiWorkerPool : ITaskApiWorkerPool
    {
        private readonly ILogger<DefaultTaskApiWorkerPool> _logger;
        private readonly INetworkAutoDiscovery _networkAutoDiscovery;
        private readonly WorkerCoreRequestCollection<ITaskApiWorkerCore> _requestCollection;
        private readonly MutexSlim _disposing;
        private readonly CancellationTokenSource _disposedCancellationTokenSource;
        private bool _disposed;
        private readonly string? _localWorkerUniqueId;
        private readonly TaskApiWorkerCoreProvider? _localWorkerCoreProvider;
        private readonly SingleSourceWorkerCoreRequestFulfiller<ITaskApiWorkerCore>? _localWorkerFulfiller;
        private readonly WorkerCoreProviderCollection<ITaskApiWorkerCore>? _remoteWorkerCoreProviderCollection;
        private readonly MultipleSourceWorkerCoreRequestFulfiller<ITaskApiWorkerCore>? _remoteWorkerFulfiller;
        private readonly Task? _remoteWorkerDiscoveryTask;

        public DefaultTaskApiWorkerPool(
            ILogger<DefaultTaskApiWorkerPool> logger,
            INetworkAutoDiscovery networkAutoDiscovery,
            TaskApiWorkerPoolConfiguration poolConfiguration)
        {
            _logger = logger;
            _networkAutoDiscovery = networkAutoDiscovery;
            _requestCollection = new WorkerCoreRequestCollection<ITaskApiWorkerCore>();
            _disposing = new MutexSlim();
            _disposedCancellationTokenSource = new CancellationTokenSource();
            _disposed = false;

            if (poolConfiguration.LocalWorker != null)
            {
                _localWorkerUniqueId = poolConfiguration.LocalWorker.UniqueId;
                _localWorkerCoreProvider = new TaskApiWorkerCoreProvider(
                    _logger,
                    poolConfiguration.LocalWorker.Client,
                    poolConfiguration.LocalWorker.UniqueId,
                    poolConfiguration.LocalWorker.DisplayName);
                _localWorkerFulfiller = new SingleSourceWorkerCoreRequestFulfiller<ITaskApiWorkerCore>(
                    _logger,
                    _requestCollection,
                    _localWorkerCoreProvider,
                    true);
            }

            if (poolConfiguration.EnableNetworkAutoDiscovery)
            {
                _remoteWorkerCoreProviderCollection = new WorkerCoreProviderCollection<ITaskApiWorkerCore>();
                _remoteWorkerFulfiller = new MultipleSourceWorkerCoreRequestFulfiller<ITaskApiWorkerCore>(
                    _logger,
                    _requestCollection,
                    _remoteWorkerCoreProviderCollection,
                    false);
                _remoteWorkerDiscoveryTask = Task.Run(DiscoverRemoteWorkersAsync);
            }
        }

        private async Task DiscoverRemoteWorkersAsync()
        {
            while (!_disposedCancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await foreach (var entry in _networkAutoDiscovery.DiscoverServicesAsync(
                        $"_{WorkerDiscoveryConstants.OpenGEPlatformIdentifier}{WorkerDiscoveryConstants.OpenGEProtocolVersion}-openge._tcp.local",
                        _disposedCancellationTokenSource.Token))
                    {
                        var workerUniqueId = entry.ServiceName.Split('.')[0];
                        if (_localWorkerUniqueId == workerUniqueId)
                        {
                            // This is our local worker, which we're already connected to directly.
                            continue;
                        }

                        if (await _remoteWorkerCoreProviderCollection!.HasAsync(workerUniqueId))
                        {
                            // We already have this worker. Do not connect to it again.
                            continue;
                        }

                        var factory = new StaticResolverFactory(
                            addr => entry.TargetAddressList.Select(
                                x => new BalancerAddress(x.ToString(), entry.TargetPort)));
                        var services = new ServiceCollection();
                        services.AddSingleton<ResolverFactory>(factory);

                        // Try to ping this remote worker.
                        var taskApi = new TaskApi.TaskApiClient(
                            GrpcChannel.ForAddress(
                                $"static:///{entry.ServiceName}",
                                new GrpcChannelOptions
                                {
                                    HttpHandler = new SocketsHttpHandler
                                    {
                                        EnableMultipleHttp2Connections = true,
                                        ConnectTimeout = TimeSpan.FromSeconds(1)
                                    },
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
                                deadline: DateTime.UtcNow.AddSeconds(10));
                            usable = true;
                        }
                        catch
                        {
                        }
                        if (usable)
                        {
                            var newProvider = new TaskApiWorkerCoreProvider(
                                _logger,
                                taskApi,
                                workerUniqueId,
                                entry.TargetHostname);
                            await newProvider.OnTaskApiDisconnected.AddAsync(OnRemoteWorkerDisconnectedAsync);
                            await _remoteWorkerCoreProviderCollection.AddAsync(newProvider);
                            _logger.LogInformation($"Discovered remote worker {entry.TargetHostname} at '{entry.ServiceName}'.");
                        }
                        else
                        {
                            _logger.LogWarning($"Discovered remote worker at {entry.ServiceName}, but it wasn't usable based on the gRPC ping request. The addresses we tried were: {string.Join(", ", entry.TargetAddressList.Select(x => x.ToString()))}");
                        }
                    }
                }
                catch (OperationCanceledException) when (_disposedCancellationTokenSource.IsCancellationRequested)
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

        private async Task OnRemoteWorkerDisconnectedAsync(
            IWorkerCoreProvider<ITaskApiWorkerCore> provider,
            CancellationToken token)
        {
            var castedProvider = (TaskApiWorkerCoreProvider)provider;
            _logger.LogInformation($"Notified that remote worker {castedProvider.DisplayName} is going away.");
            await _remoteWorkerCoreProviderCollection!.RemoveAsync(provider);
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
            using var _ = await _disposing.WaitAsync();

            _logger.LogInformation("Worker pool is now shutting down.");

            if (!_disposed)
            {
                if (_localWorkerFulfiller != null)
                {
                    _logger.LogInformation("Waiting for local worker request fulfiller to dispose...");
                    await _localWorkerFulfiller.DisposeAsync();
                }
                if (_remoteWorkerFulfiller != null)
                {
                    _logger.LogInformation("Waiting for remote worker request fulfiller to dispose...");
                    await _remoteWorkerFulfiller.DisposeAsync();
                }
                if (_remoteWorkerDiscoveryTask != null)
                {
                    try
                    {
                        _logger.LogInformation("Waiting for remote worker discovery task to complete...");
                        _disposedCancellationTokenSource.Cancel();
                        await _remoteWorkerDiscoveryTask;
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
