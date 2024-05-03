namespace UET.Commands.Internal.OpenGEPreprocessorCache
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Component.PreprocessorCache.OnDemand;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using UET.Services;

    internal sealed class OpenGEPreprocessorCacheClientUnresolvedCommand
    {
        internal sealed class Options
        {
            public Option<FileInfo> File;

            public Options()
            {
                File = new Option<FileInfo>("--file") { IsRequired = true };
            }
        }

        public static Command CreateUnresolvedCommand()
        {
            var options = new Options();
            var command = new Command("unresolved");
            command.AddAllOptions(options);
            command.AddCommonHandler<OpenGEPreprocessorCacheClientUnresolvedCommandInstance>(options);
            return command;
        }

        private sealed class OpenGEPreprocessorCacheClientUnresolvedCommandInstance : ICommandInstance
        {
            private readonly ILogger<OpenGEPreprocessorCacheClientUnresolvedCommandInstance> _logger;
            private readonly IPreprocessorCacheFactory _preprocessorCacheFactory;
            private readonly ISelfLocation _selfLocation;
            private readonly Options _options;

            public OpenGEPreprocessorCacheClientUnresolvedCommandInstance(
                ILogger<OpenGEPreprocessorCacheClientUnresolvedCommandInstance> logger,
                IPreprocessorCacheFactory preprocessorCacheFactory,
                ISelfLocation selfLocation,
                Options options)
            {
                _logger = logger;
                _preprocessorCacheFactory = preprocessorCacheFactory;
                _selfLocation = selfLocation;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var preprocessorCache = _preprocessorCacheFactory.CreateOnDemandCache(new Redpoint.ProcessExecution.ProcessSpecification
                {
                    FilePath = _selfLocation.GetUETLocalLocation(),
                    Arguments = new Redpoint.ProcessExecution.LogicalProcessArgument[]
                    {
                        "internal",
                        "openge-preprocessor-cache"
                    }
                });

                await preprocessorCache.EnsureAsync().ConfigureAwait(false);

                var st = Stopwatch.StartNew();
                var result = await preprocessorCache.GetUnresolvedDependenciesAsync(
                    context.ParseResult.GetValueForOption(_options.File)!.FullName,
                    context.GetCancellationToken()).ConfigureAwait(false);
                var ms = st.ElapsedMilliseconds;

                _logger.LogInformation($"Initial request completed in: {ms}ms");
                {
                    st = Stopwatch.StartNew();
                    _ = await preprocessorCache.GetUnresolvedDependenciesAsync(
                        context.ParseResult.GetValueForOption(_options.File)!.FullName,
                        context.GetCancellationToken()).ConfigureAwait(false);
                    ms = st.ElapsedMilliseconds;
                }
                _logger.LogInformation($"Subsequent request completed in: {ms}ms");
                _logger.LogInformation($"Resolution completed in: {result.ResolutionTimeMs}ms");
                _logger.LogInformation($"Cache status: {result.CacheStatus}");
                _logger.LogInformation($"File last written: {result.Result.FileLastWriteTicks}");
                _logger.LogInformation($"{result.Result.Conditions.Count} conditions:");
                foreach (var condition in result.Result.Conditions)
                {
                    _logger.LogInformation($"  {condition}");
                }
                _logger.LogInformation($"{result.Result.Directives.Count} directives:");
                foreach (var directive in result.Result.Directives)
                {
                    _logger.LogInformation($"  {directive}");
                }

                return 0;
            }
        }
    }
}
