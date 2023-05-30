namespace UET.Commands.Build
{
    using System;
    using System.CommandLine;
    using System.Threading.Tasks;
    using UET.Commands.EngineSpec;
    using System.CommandLine.Invocation;
    using Microsoft.Extensions.Logging;

    internal class BuildCommand
    {
        internal class Options
        {
            public Option<EngineSpec> Engine;
            public Option<PathSpec> Path;
            public Option<DistributionSpec?> Distribution;

            public Options()
            {
                Path = new Option<PathSpec>(
                    "--path",
                    description: "The directory path that contains a .uproject file, a .uplugin file, or a BuildConfig.json file. If this parameter isn't provided, defaults to the current working directory.",
                    parseArgument: PathSpec.ParsePathSpec,
                    isDefault: true);
                Path.AddAlias("-p");
                Path.Arity = ArgumentArity.ExactlyOne;

                Distribution = new Option<DistributionSpec?>(
                    "--distribution",
                    description: "The distribution to build if targeting a BuildConfig.json file.",
                    parseArgument: DistributionSpec.ParseDistributionSpec(Path),
                    isDefault: true);
                Distribution.AddAlias("-d");
                Distribution.Arity = ArgumentArity.ExactlyOne;

                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine to use for the build.",
                    parseArgument: EngineSpec.ParseEngineSpec(Path, Distribution),
                    isDefault: true);
                Engine.AddAlias("-e");
                Engine.Arity = ArgumentArity.ExactlyOne;
            }
        }

        public static Command CreateBuildCommand()
        {
            var options = new Options();
            var command = new Command("build", "Build an Unreal Engine project or plugin.");
            command.AddAllOptions(options);
            command.AddCommonHandler<BuildCommandInstance>(options);
            return command;
        }

        private class BuildCommandInstance : ICommandInstance
        {
            private readonly ILogger<BuildCommandInstance> _logger;
            private readonly Options _options;

            public BuildCommandInstance(
                ILogger<BuildCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var engine = context.ParseResult.GetValueForOption(_options.Engine);
                var path = context.ParseResult.GetValueForOption(_options.Path);
                var distribution = context.ParseResult.GetValueForOption(_options.Distribution);

                _logger.LogInformation($"--engine:       {engine}");
                _logger.LogInformation($"--path:         {path}");
                _logger.LogInformation($"--distribution: {(distribution == null ? "(not set)" : distribution)}");

                _logger.LogInformation("not implemented yet");
                return 0;
            }
        }
    }
}
