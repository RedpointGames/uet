namespace UET.Services
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.Agent;
    using Redpoint.OpenGE.Component.PreprocessorCache;
    using Redpoint.OpenGE.Component.PreprocessorCache.OnDemand;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.OpenGE;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class DefaultOpenGEProvider : IOpenGEProvider
    {
        private readonly Redpoint.Concurrency.Semaphore _setupSemaphore = new Redpoint.Concurrency.Semaphore(1);
        private readonly ILogger<DefaultOpenGEProvider> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IGrpcPipeFactory _grpcPipeFactory;
        private readonly IOpenGEAgentFactory _openGEAgentFactory;
        private readonly IPreprocessorCacheFactory _preprocessorCacheFactory;
        private readonly ISelfLocation _selfLocation;
        private OpenGEEnvironmentInfo? _environmentInfo;
        private IOpenGEAgent? _agent;
        private IPreprocessorCache? _onDemandCache;
        private readonly Redpoint.Concurrency.Semaphore _onDemandCacheSemaphore = new Redpoint.Concurrency.Semaphore(1);

        public DefaultOpenGEProvider(
            ILogger<DefaultOpenGEProvider> logger,
            IPathResolver pathResolver,
            IGrpcPipeFactory grpcPipeFactory,
            IOpenGEAgentFactory openGEAgentFactory,
            IPreprocessorCacheFactory preprocessorCacheFactory,
            ISelfLocation selfLocation)
        {
            _logger = logger;
            _pathResolver = pathResolver;
            _grpcPipeFactory = grpcPipeFactory;
            _openGEAgentFactory = openGEAgentFactory;
            _preprocessorCacheFactory = preprocessorCacheFactory;
            _selfLocation = selfLocation;
        }

        public async Task<OpenGEEnvironmentInfo> GetOpenGEEnvironmentInfo()
        {
            if (_environmentInfo != null)
            {
                return _environmentInfo;
            }

            await _setupSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_environmentInfo != null)
                {
                    return _environmentInfo;
                }

                // See if xgConsole is already on the PATH.
                /*
                bool hasExistingXgConsole = false;
                try
                {
                    _ = await _pathResolver.ResolveBinaryPath(OperatingSystem.IsLinux() ? "ib_console" : "xgConsole");
                    hasExistingXgConsole = true;
                }
                catch (FileNotFoundException)
                {
                }
                if (hasExistingXgConsole)
                {
                    // This computer already has XGE or OpenGE installed globally,
                    // so we don't need to set it up.
                    _environmentInfo = new OpenGEEnvironmentInfo
                    {
                        ShouldUseOpenGE = false,
                        UsingSystemWideDaemon = true,
                        PerProcessDispatcherPipeName = string.Empty,
                    };
                    return _environmentInfo;
                }
                */

                // See if we're running OpenGE system-wide. We do this by trying
                // to connect to the daemon on the well-known system-wide pipe name,
                // and if it succeeds, then we know it's running.
                var jobClient = _grpcPipeFactory.CreateClient(
                    "OpenGE",
                    GrpcPipeNamespace.Computer,
                    channel => new JobApi.JobApiClient(channel));
                var usingSystemWide = false;
                try
                {
                    await jobClient.PingJobServiceAsync(
                        new PingJobServiceRequest(),
                        deadline: DateTime.UtcNow.AddSeconds(5));
                    usingSystemWide = true;
                }
                catch (RpcException)
                {
                }
                if (usingSystemWide)
                {
                    _logger.LogInformation("Using system-wide OpenGE daemon for executing tasks.");
                    _environmentInfo = new OpenGEEnvironmentInfo
                    {
                        ShouldUseOpenGE = true,
                        UsingSystemWideDaemon = true,
                        PerProcessDispatcherPipeName = string.Empty,
                    };
                    return _environmentInfo;
                }

                // If we're not already running inside a process tree that's had OpenGE set up, 
                // just re-use the same dispatcher pipe name that we have available.
                var existingPipeName = Environment.GetEnvironmentVariable("UET_XGE_SHIM_PIPE_NAME");
                if (!string.IsNullOrWhiteSpace(existingPipeName))
                {
                    _logger.LogInformation("Using inherited OpenGE pipe for executing tasks.");
                    _environmentInfo = new OpenGEEnvironmentInfo
                    {
                        ShouldUseOpenGE = true,
                        UsingSystemWideDaemon = false,
                        PerProcessDispatcherPipeName = existingPipeName,
                    };
                    return _environmentInfo;
                }

                // The system-wide daemon is not available. Create an OpenGE
                // agent within our process.
                _logger.LogInformation("Launching in-process version of OpenGE for executing tasks.");
                _agent = _openGEAgentFactory.CreateAgent(false, true);
                await _agent.StartAsync().ConfigureAwait(false);
                _environmentInfo = new OpenGEEnvironmentInfo
                {
                    ShouldUseOpenGE = true,
                    UsingSystemWideDaemon = false,
                    PerProcessDispatcherPipeName = _agent.DispatcherConnectionString,
                };
                return _environmentInfo;
            }
            finally
            {
                _setupSemaphore.Release();
            }
        }

        public async Task<IPreprocessorCache> GetPreprocessorCacheAsync()
        {
            if (_environmentInfo == null)
            {
                await GetOpenGEEnvironmentInfo().ConfigureAwait(false);
            }

            // @note: The daemon no longer goes through this code
            // path, because the daemon has it's own service bindings
            // that mean IPreprocessorCacheAccessor is provided by
            // OpenGEHostedService. Therefore, the only code that
            // calls this function requires the on-demand cache.

            if (_onDemandCache != null)
            {
                return _onDemandCache;
            }
            await _onDemandCacheSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_onDemandCache != null)
                {
                    return _onDemandCache;
                }
                _onDemandCache = _preprocessorCacheFactory.CreateOnDemandCache(
                    new ProcessSpecification
                    {
                        FilePath = _selfLocation.GetUETLocalLocation(),
                        Arguments = new LogicalProcessArgument[]
                        {
                            "internal",
                            "openge-preprocessor-cache"
                        }
                    });
                return _onDemandCache;
            }
            finally
            {
                _onDemandCacheSemaphore.Release();
            }
        }

        public Task StartAsync(CancellationToken shutdownCancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            await _setupSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_agent != null)
                {
                    await _agent.StopAsync().ConfigureAwait(false);
                    _agent = null;
                }
            }
            finally
            {
                _setupSemaphore.Release();
            }
        }
    }
}
