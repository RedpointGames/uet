namespace UET.Commands.Internal.OpenGEPreprocessorCache
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.PreprocessorCache;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using UET.Services;

    internal class OpenGEPreprocessorCacheClientCommand
    {
        internal class Options
        {
            public Option<FileInfo> File;

            public Options()
            {
                File = new Option<FileInfo>("--file") { IsRequired = true };
            }
        }

        public static Command CreateOpenGEPreprocessorCacheClientCommand()
        {
            var options = new Options();
            var command = new Command("openge-preprocessor-cache-client");
            command.AddAllOptions(options);
            command.AddCommonHandler<OpenGEPreprocessorCacheClientCommandInstance>(options);
            return command;
        }

        private class OpenGEPreprocessorCacheClientCommandInstance : ICommandInstance
        {
            private readonly ILogger<OpenGEPreprocessorCacheClientCommandInstance> _logger;
            private readonly IPreprocessorCacheFactory _preprocessorCacheFactory;
            private readonly ISelfLocation _selfLocation;
            private readonly Options _options;

            public OpenGEPreprocessorCacheClientCommandInstance(
                ILogger<OpenGEPreprocessorCacheClientCommandInstance> logger,
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
                var preprocessorCache = _preprocessorCacheFactory.CreatePreprocessorCache(new Redpoint.ProcessExecution.ProcessSpecification
                {
                    FilePath = _selfLocation.GetUETLocalLocation(),
                    Arguments = new[]
                    {
                        "internal",
                        "openge-preprocessor-cache"
                    }
                });

                await preprocessorCache.EnsureConnectedAsync();

                var st = Stopwatch.StartNew();
                var result = await preprocessorCache.GetUnresolvedDependenciesAsync(
                    context.ParseResult.GetValueForOption(_options.File)!.FullName,
                    context.GetCancellationToken());
                var ms = st.ElapsedMilliseconds;

                _logger.LogInformation($"Initial request completed in: {ms}ms");
                {
                    st = Stopwatch.StartNew();
                    _ = await preprocessorCache.GetUnresolvedDependenciesAsync(
                        context.ParseResult.GetValueForOption(_options.File)!.FullName,
                        context.GetCancellationToken());
                    ms = st.ElapsedMilliseconds;
                }
                _logger.LogInformation($"Subsequent request completed in: {ms}ms");
                _logger.LogInformation($"Resolution completed in: {result.ResolutionTimeMs}ms");
                _logger.LogInformation($"Cache status: {result.CacheStatus}");
                _logger.LogInformation($"File last written: {result.Result.FileLastWriteTicks}");
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
