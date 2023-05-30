namespace UET.Commands.Build
{
    using System;
    using System.CommandLine;
    using System.Threading.Tasks;
    using UET.Commands.EngineSpec;
    using System.CommandLine.Invocation;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.BuildPipeline.Executors;
    using System.Net.Http.Headers;
    using Redpoint.UET.Configuration.Project;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Engine;
    using Redpoint.UET.BuildPipeline.Executors.Local;
    using Redpoint.ProcessExecution;

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
            command.AddCommonHandler<BuildCommandInstance>(
                options,
                services =>
                {
                    services.AddSingleton<IBuildSpecificationGenerator, DefaultBuildSpecificationGenerator>();
                });
            return command;
        }

        private class BuildCommandInstance : ICommandInstance
        {
            private readonly ILogger<BuildCommandInstance> _logger;
            private readonly Options _options;
            private readonly IBuildSpecificationGenerator _buildSpecificationGenerator;
            private readonly LocalBuildExecutorFactory _localBuildExecutorFactory;

            public BuildCommandInstance(
                ILogger<BuildCommandInstance> logger,
                Options options,
                IBuildSpecificationGenerator buildSpecificationGenerator,
                LocalBuildExecutorFactory localBuildExecutorFactory)
            {
                _logger = logger;
                _options = options;
                _buildSpecificationGenerator = buildSpecificationGenerator;
                _localBuildExecutorFactory = localBuildExecutorFactory;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var engine = context.ParseResult.GetValueForOption(_options.Engine)!;
                var path = context.ParseResult.GetValueForOption(_options.Path)!;
                var distribution = context.ParseResult.GetValueForOption(_options.Distribution);

                _logger.LogInformation($"--engine:       {engine}");
                _logger.LogInformation($"--path:         {path}");
                _logger.LogInformation($"--distribution: {(distribution == null ? "(not set)" : distribution)}");

                BuildEngineSpecification engineSpec;
                switch (engine.Type)
                {
                    case EngineSpecType.UEFSPackageTag:
                        engineSpec = BuildEngineSpecification.ForUEFSPackageTag(engine.UEFSPackageTag!);
                        break;
                    case EngineSpecType.Path:
                        engineSpec = BuildEngineSpecification.ForPath(engine.Path!);
                        break;
                    default:
                        throw new NotSupportedException();
                }

                BuildSpecification buildSpec;
                switch (path!.Type)
                {
                    case PathSpecType.BuildConfig:
                        switch (distribution!.Distribution)
                        {
                            case BuildConfigProjectDistribution d:
                                buildSpec = _buildSpecificationGenerator.BuildConfigProjectToBuildSpec(
                                    engineSpec,
                                    d,
                                    path.DirectoryPath);
                                break;
                            case BuildConfigPluginDistribution d:
                                buildSpec = _buildSpecificationGenerator.BuildConfigPluginToBuildSpec(
                                    engineSpec,
                                    d);
                                break;
                            case BuildConfigEngineDistribution d:
                                buildSpec = _buildSpecificationGenerator.BuildConfigEngineToBuildSpec(
                                    engineSpec,
                                    d);
                                break;
                            default:
                                throw new NotSupportedException();
                        }
                        break;
                    case PathSpecType.UProject:
                        buildSpec = _buildSpecificationGenerator.ProjectPathSpecToBuildSpec(
                            engineSpec,
                            path);
                        break;
                    case PathSpecType.UPlugin:
                        buildSpec = _buildSpecificationGenerator.PluginPathSpecToBuildSpec(
                            engineSpec,
                            path);
                        break;
                    default:
                        throw new NotSupportedException();
                }

                var executor = _localBuildExecutorFactory.CreateExecutor();

                var buildResult = await executor.ExecuteBuildAsync(
                    buildSpec,
                    new LoggerBasedBuildExecutionEvents(_logger),
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());
                return buildResult;
            }
        }
    }
}
