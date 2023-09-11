namespace UET.Commands.List
{
    using Redpoint.Uet.Configuration.Engine;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using UET.Commands.EngineSpec;
    using UET.BuildConfig;

    internal sealed class ListCommand
    {
        internal sealed class Options
        {
            public Option<PathSpec> Path;

            public Options()
            {
                Path = new Option<PathSpec>(
                    "--path",
                    description: "The directory path that contains a BuildConfig.json file. If this parameter isn't provided, defaults to the current working directory.",
                    parseArgument: PathSpec.ParsePathSpec,
                    isDefault: true);
                Path.AddAlias("-p");
                Path.Arity = ArgumentArity.ExactlyOne;
            }
        }

        public static Command CreateListCommand()
        {
            var options = new Options();
            var command = new Command("list", "List the distributions in a BuildConfig.json file.");
            command.AddAllOptions(options);
            command.AddCommonHandler<ListCommandInstance>(
                options,
                services =>
                {
                });
            return command;
        }

        private sealed class ListCommandInstance : ICommandInstance
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly ILogger<ListCommandInstance> _logger;
            private readonly Options _options;

            public ListCommandInstance(
                IServiceProvider serviceProvider,
                ILogger<ListCommandInstance> logger,
                Options options)
            {
                _serviceProvider = serviceProvider;
                _logger = logger;
                _options = options;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                var path = context.ParseResult.GetValueForOption(_options.Path)!;

                if (path.Type != PathSpecType.BuildConfig)
                {
                    _logger.LogError("You can only list distributions for a BuildConfig.json file.");
                    return Task.FromResult(1);
                }

                var loadResult = BuildConfigLoader.TryLoad(
                    _serviceProvider,
                    Path.Combine(path.DirectoryPath, "BuildConfig.json"));
                if (loadResult.Success)
                {
                    switch (loadResult.BuildConfig!)
                    {
                        case BuildConfigProject buildConfigProject:
                            {
                                _logger.LogInformation($"{buildConfigProject.Distributions.Count} project distribution{(buildConfigProject.Distributions.Count == 1 ? "" : "s")} found{(buildConfigProject.Distributions.Count == 0 ? "." : ":")}");
                                foreach (var distribution in buildConfigProject.Distributions)
                                {
                                    _logger.LogInformation(distribution.Name);
                                }
                            }
                            break;
                        case BuildConfigPlugin buildConfigPlugin:
                            {
                                _logger.LogInformation($"{buildConfigPlugin.Distributions.Count} plugin distribution{(buildConfigPlugin.Distributions.Count == 1 ? "" : "s")} found{(buildConfigPlugin.Distributions.Count == 0 ? "." : ":")}");
                                foreach (var distribution in buildConfigPlugin.Distributions)
                                {
                                    _logger.LogInformation(distribution.Name);
                                }
                            }
                            break;
                        case BuildConfigEngine buildConfigEngine:
                            {
                                _logger.LogInformation($"{buildConfigEngine.Distributions.Count} engine distribution{(buildConfigEngine.Distributions.Count == 1 ? "" : "s")} found{(buildConfigEngine.Distributions.Count == 0 ? "." : ":")}");
                                foreach (var distribution in buildConfigEngine.Distributions)
                                {
                                    _logger.LogInformation(distribution.Name);
                                }
                            }
                            break;
                    }

                    return Task.FromResult(0);
                }

                foreach (var line in loadResult.ErrorList)
                {
                    _logger.LogError(line);
                }
                return Task.FromResult(1);
            }
        }
    }
}