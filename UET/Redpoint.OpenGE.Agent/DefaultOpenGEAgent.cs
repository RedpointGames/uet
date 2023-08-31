namespace Redpoint.OpenGE.Agent
{
    using Grpc.Net.Client;
    using Microsoft.AspNetCore.Hosting.Server;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.Component.Dispatcher;
    using Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Component.PreprocessorCache;
    using Redpoint.OpenGE.Component.PreprocessorCache.OnDemand;
    using Redpoint.OpenGE.Component.Worker;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal class DefaultOpenGEAgent : IOpenGEAgent
    {
        private readonly ILogger<DefaultOpenGEAgent> _logger;
        private readonly IDispatcherComponentFactory _dispatcherComponentFactory;
        private readonly IWorkerComponentFactory _workerComponentFactory;
        private readonly ITaskApiWorkerPoolFactory _taskApiWorkerPoolFactory;
        private readonly IPreprocessorCacheFactory _preprocessorCacheFactory;
        private readonly IGrpcPipeFactory _grpcPipeFactory;
        private readonly bool _runAsSystemWideService;
        private readonly bool _runLocalWorker;
        private CancellationTokenSource _shutdownCancellationTokenSource;
        private AbstractInProcessPreprocessorCache? _preprocessorComponent;
        private IWorkerComponent? _workerComponent;
        private TaskApi.TaskApiClient? _localWorkerClient;
        private ITaskApiWorkerPool? _taskApiWorkerPool;
        private IDispatcherComponent? _dispatcherComponent;
        private IGrpcPipeServer<AbstractInProcessPreprocessorCache>? _preprocessorServer;

        public DefaultOpenGEAgent(
            ILogger<DefaultOpenGEAgent> logger,
            IDispatcherComponentFactory dispatcherComponentFactory,
            IWorkerComponentFactory workerComponentFactory,
            ITaskApiWorkerPoolFactory taskApiWorkerPoolFactory,
            IPreprocessorCacheFactory preprocessorCacheFactory,
            IGrpcPipeFactory grpcPipeFactory,
            bool runAsSystemWideService,
            bool runLocalWorker)
        {
            _logger = logger;
            _dispatcherComponentFactory = dispatcherComponentFactory;
            _workerComponentFactory = workerComponentFactory;
            _taskApiWorkerPoolFactory = taskApiWorkerPoolFactory;
            _preprocessorCacheFactory = preprocessorCacheFactory;
            _grpcPipeFactory = grpcPipeFactory;
            _runAsSystemWideService = runAsSystemWideService;
            _runLocalWorker = runLocalWorker;
            _shutdownCancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            if (_runAsSystemWideService)
            {
                _preprocessorComponent = _preprocessorCacheFactory.CreateInProcessCache();
                await _preprocessorComponent.EnsureAsync();
                _preprocessorServer = _grpcPipeFactory.CreateServer(
                    "OpenGEPreprocessorCache",
                    GrpcPipeNamespace.Computer,
                    _preprocessorComponent);
                await _preprocessorServer.StartAsync();
            }

            if (_runLocalWorker)
            {
                _workerComponent = _workerComponentFactory.Create(!_runAsSystemWideService);
                await _workerComponent.StartAsync(_shutdownCancellationTokenSource.Token);
                _localWorkerClient = new TaskApi.TaskApiClient(
                    GrpcChannel.ForAddress($"http://127.0.0.1:{_workerComponent.ListeningPort}"));
                _taskApiWorkerPool = _taskApiWorkerPoolFactory.CreateWorkerPool(new TaskApiWorkerPoolConfiguration
                {
                    EnableNetworkAutoDiscovery = true,
                    LocalWorker = new TaskApiWorkerPoolConfigurationLocalWorker
                    {
                        DisplayName = _workerComponent.WorkerDisplayName,
                        UniqueId = _workerComponent.WorkerUniqueId,
                        Client = _localWorkerClient,
                    }
                });
            }
            else
            {
                _taskApiWorkerPool = _taskApiWorkerPoolFactory.CreateWorkerPool(new TaskApiWorkerPoolConfiguration
                {
                    EnableNetworkAutoDiscovery = true,
                    LocalWorker = null,
                });
            }

            _dispatcherComponent = _dispatcherComponentFactory.Create(
                _taskApiWorkerPool,
                _runAsSystemWideService ? "OpenGE" : null);
            await _dispatcherComponent.StartAsync(_shutdownCancellationTokenSource.Token);
        }

        public string DispatcherConnectionString
        {
            get
            {
                if (_dispatcherComponent == null)
                {
                    throw new InvalidOperationException("The OpenGE agent must be started first.");
                }
                return _dispatcherComponent.GetConnectionString();
            }
        }

        public async Task StopAsync()
        {
            _shutdownCancellationTokenSource.Cancel();
            _shutdownCancellationTokenSource = new CancellationTokenSource();
            if (_dispatcherComponent != null)
            {
                _logger.LogTrace("Agent is stopping dispatcher component...");
                await _dispatcherComponent.StopAsync();
                _dispatcherComponent = null;
            }
            if (_taskApiWorkerPool != null)
            {
                _logger.LogTrace("Agent is stopping worker pool...");
                await _taskApiWorkerPool.DisposeAsync();
                _taskApiWorkerPool = null;
            }
            _localWorkerClient = null;
            if (_workerComponent != null)
            {
                _logger.LogTrace("Agent is stopping worker component...");
                await _workerComponent.StopAsync();
                _workerComponent = null;
            }
            if (_preprocessorComponent != null)
            {
                _logger.LogTrace("Agent is stopping preprocessor component...");
                await _preprocessorComponent.DisposeAsync();
                _preprocessorComponent = null;
            }
            if (_preprocessorServer != null)
            {
                _logger.LogTrace("Agent is stopping preprocessor server...");
                await _preprocessorServer.StopAsync();
                _preprocessorServer = null;
            }
        }

        public Task<IPreprocessorCache> GetPreprocessorCacheAsync()
        {
            if (_preprocessorComponent == null)
            {
                throw new InvalidOperationException("GetPreprocessorCache called on IOpenGEAgent, but runAsSystemWideService == false.");
            }
            return Task.FromResult<IPreprocessorCache>(_preprocessorComponent);
        }
    }
}