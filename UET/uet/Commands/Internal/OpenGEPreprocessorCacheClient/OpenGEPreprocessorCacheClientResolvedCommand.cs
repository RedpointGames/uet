namespace UET.Commands.Internal.OpenGEPreprocessorCache
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Component.PreprocessorCache.OnDemand;
    using Redpoint.OpenGE.Protocol;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using UET.Services;

    internal class OpenGEPreprocessorCacheClientResolvedCommand
    {
        internal class Options
        {
            public Option<FileInfo> File;
            public Option<FileInfo[]> ForceIncludesFromPch;
            public Option<FileInfo[]> ForceIncludes;
            public Option<DirectoryInfo[]> IncludeDirectories;
            public Option<string[]> GlobalDefinitions;

            public Options()
            {
                File = new Option<FileInfo>("--file") { IsRequired = true };
                ForceIncludesFromPch = new Option<FileInfo[]>("--force-include-from-pch");
                ForceIncludes = new Option<FileInfo[]>("--force-include");
                IncludeDirectories = new Option<DirectoryInfo[]>("-i");
                GlobalDefinitions = new Option<string[]>("-D");
            }
        }

        public static Command CreateResolvedCommand()
        {
            var options = new Options();
            var command = new Command("resolved");
            command.AddAllOptions(options);
            command.AddCommonHandler<OpenGEPreprocessorCacheClientResolvedCommandInstance>(options);
            return command;
        }

        private class OpenGEPreprocessorCacheClientResolvedCommandInstance : ICommandInstance
        {
            private readonly ILogger<OpenGEPreprocessorCacheClientResolvedCommandInstance> _logger;
            private readonly IPreprocessorCacheFactory _preprocessorCacheFactory;
            private readonly ISelfLocation _selfLocation;
            private readonly Options _options;

            public OpenGEPreprocessorCacheClientResolvedCommandInstance(
                ILogger<OpenGEPreprocessorCacheClientResolvedCommandInstance> logger,
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
                    Arguments = new[]
                    {
                        "internal",
                        "openge-preprocessor-cache"
                    }
                });

                await preprocessorCache.EnsureAsync();

                async Task<PreprocessorResolutionResultWithTimingMetadata> InvokeAsync()
                {
                    return await preprocessorCache!.GetResolvedDependenciesAsync(
                        context.ParseResult.GetValueForOption(_options.File)!.FullName,
                        (context.ParseResult.GetValueForOption(_options.ForceIncludesFromPch) ?? Array.Empty<FileInfo>()).Select(x => x.FullName).ToArray(),
                        (context.ParseResult.GetValueForOption(_options.ForceIncludes) ?? Array.Empty<FileInfo>()).Select(x => x.FullName).ToArray(),
                        (context.ParseResult.GetValueForOption(_options.IncludeDirectories) ?? Array.Empty<DirectoryInfo>()).Select(x => x.FullName).ToArray(),
                        (context.ParseResult.GetValueForOption(_options.GlobalDefinitions) ?? Array.Empty<string>()).Select(x =>
                        {
                            var c = x.Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (c.Length == 1)
                            {
                                return new KeyValuePair<string, string>(c[0], "1");
                            }
                            else
                            {
                                return new KeyValuePair<string, string>(c[0], c[1]);
                            }
                        }).ToDictionary(k => k.Key, v => v.Value),
                        DateTimeOffset.UtcNow.Ticks,
                        context.GetCancellationToken());
                }

                var st = Stopwatch.StartNew();
                var result = await InvokeAsync();
                var ms = st.ElapsedMilliseconds;

                _logger.LogInformation($"Initial request completed in: {ms}ms");
                {
                    st = Stopwatch.StartNew();
                    _ = await InvokeAsync();
                    ms = st.ElapsedMilliseconds;
                }
                _logger.LogInformation($"Subsequent request completed in: {ms}ms");
                _logger.LogInformation($"Resolution completed in: {result.ResolutionTimeMs}ms");
                _logger.LogInformation($"{result.DependsOnPaths.Count} dependencies:");
                foreach (var dependency in result.DependsOnPaths)
                {
                    _logger.LogInformation($"  {dependency}");
                }

                return 0;
            }
        }
    }
}
