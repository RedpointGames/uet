namespace UET.Commands.Internal.OpenGEPreprocessorCache
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.Component.PreprocessorCache;
    using Redpoint.OpenGE.Component.PreprocessorCache.OnDemand;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal sealed class OpenGEPreprocessorCacheCommand
    {
        internal sealed class Options
        {
        }

        public static Command CreateOpenGEPreprocessorCacheCommand()
        {
            var options = new Options();
            var command = new Command("openge-preprocessor-cache");
            command.AddAllOptions(options);
            command.AddCommonHandler<OpenGEPreprocessorCacheCommandInstance>(options);
            return command;
        }

        private sealed class OpenGEPreprocessorCacheCommandInstance : ICommandInstance
        {
            private readonly IGrpcPipeFactory _grpcPipeFactory;
            private readonly IPreprocessorCacheFactory _preprocessorCacheFactory;
            private readonly ILogger<OpenGEPreprocessorCacheCommandInstance> _logger;

            public OpenGEPreprocessorCacheCommandInstance(
                IGrpcPipeFactory grpcPipeFactory,
                IPreprocessorCacheFactory preprocessorCacheFactory,
                ILogger<OpenGEPreprocessorCacheCommandInstance> logger)
            {
                _grpcPipeFactory = grpcPipeFactory;
                _preprocessorCacheFactory = preprocessorCacheFactory;
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                IGrpcPipeServer<AbstractInProcessPreprocessorCache>? server = null;
                try
                {
                    _logger.LogInformation("Starting OpenGE preprocessor cache...");

                    await using (_preprocessorCacheFactory.CreateInProcessCache().AsAsyncDisposable(out var cache).ConfigureAwait(false))
                    {
                        await cache.EnsureAsync().ConfigureAwait(false);

                        server = _grpcPipeFactory.CreateServer(
                            "OpenGEPreprocessorCache",
                            GrpcPipeNamespace.Computer,
                            cache);
                        await server.StartAsync().ConfigureAwait(false);

                        // Run until terminated, or until we've been idle for 5 minutes.
                        while (!context.GetCancellationToken().IsCancellationRequested)
                        {
                            await Task.Delay(10000, context.GetCancellationToken()).ConfigureAwait(false);
                            if ((DateTimeOffset.UtcNow - cache.LastGrpcRequestUtc).TotalMinutes > 5)
                            {
                                _logger.LogInformation("OpenGE preprocessor cache is automatically exiting because it has been idle for more than 5 minutes.");
                                return 0;
                            }
                        }

                        return 0;
                    }
                }
                finally
                {
                    // We shutdown the server outside the reservation so that we can't run into
                    // a race condition where a new instance of the preprocessor cache exits
                    // due the reservation being held while the current one is shutting down.
                    if (server != null)
                    {
                        await server.StopAsync().ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
