namespace UET.Services
{
    using Grpc.Core;
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

    internal class DefaultOpenGEProvider : IOpenGEProvider
    {
        private readonly SemaphoreSlim _setupSemaphore = new SemaphoreSlim(1);
        private readonly IPathResolver _pathResolver;
        private readonly IGrpcPipeFactory _grpcPipeFactory;
        private readonly IOpenGEAgentFactory _openGEAgentFactory;
        private readonly IPreprocessorCacheFactory _preprocessorCacheFactory;
        private readonly ISelfLocation _selfLocation;
        private OpenGEEnvironmentInfo? _environmentInfo;
        private IOpenGEAgent? _agent;

        public DefaultOpenGEProvider(
            IPathResolver pathResolver,
            IGrpcPipeFactory grpcPipeFactory,
            IOpenGEAgentFactory openGEAgentFactory,
            IPreprocessorCacheFactory preprocessorCacheFactory,
            ISelfLocation selfLocation)
        {
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

            await _setupSemaphore.WaitAsync();
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
                _agent = _openGEAgentFactory.CreateAgent(false);
                await _agent.StartAsync();
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
            if (_environmentInfo != null)
            {
                await GetOpenGEEnvironmentInfo();
            }

            if (_agent != null)
            {
                return await _agent.GetPreprocessorCacheAsync();
            }

            return _preprocessorCacheFactory.CreateOnDemandCache(
                new ProcessSpecification
                {
                    FilePath = _selfLocation.GetUETLocalLocation(),
                    Arguments = new[]
                    {
                        "internal",
                        "openge-preprocessor-cache"
                    }
                });
        }

        public Task StartAsync(CancellationToken shutdownCancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            await _setupSemaphore.WaitAsync();
            try
            {
                if (_agent != null)
                {
                    await _agent.StopAsync();
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
