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
    using System.Diagnostics.CodeAnalysis;
    using Redpoint.UET.BuildPipeline.Executors.GitLab;

    internal class BuildCommand
    {
        internal class Options
        {
            public Option<EngineSpec> Engine;
            public Option<PathSpec> Path;
            public Option<DistributionSpec?> Distribution;
            public Option<bool> Shipping;
            public Option<string> Executor;
            public Option<string> ExecutorOutputFile;
            public Option<string?> WindowsSharedStoragePath;
            public Option<string?> MacSharedStoragePath;

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

                Shipping = new Option<bool>(
                    "--shipping",
                    description: "If set, builds for Shipping instead of Development. Only valid when not using a BuildConfig.json file to build.");
                Shipping.AddValidator(result =>
                {
                    PathSpec? pathSpec;
                    try
                    {
                        pathSpec = result.GetValueForOption(Path);
                    }
                    catch
                    {
                        result.ErrorMessage = $"Can't use --{result.Option.Name} because --{Path.Name} is invalid.";
                        return;
                    }
                    if (pathSpec == null)
                    {
                        result.ErrorMessage = $"Can't use --{result.Option.Name} because --{Path.Name} is invalid.";
                        return;
                    }
                    if (pathSpec.Type == PathSpecType.BuildConfig)
                    {
                        result.ErrorMessage = $"Can't use --{result.Option.Name} because --{Path.Name} points to a BuildConfig.json.";
                        return;
                    }
                });

                Executor = new Option<string>(
                    "--executor",
                    description: "The executor to use.",
                    getDefaultValue: () => "local");
                Executor.AddAlias("-x");
                Executor.FromAmong("local", "gitlab");

                ExecutorOutputFile = new Option<string>(
                    "--executor-output-file",
                    description: "If the executor runs the build externally (e.g. a build server), this is the path to the emitted file that should be passed as the job or build description into the build server.");

                WindowsSharedStoragePath = new Option<string?>(
                    "--windows-shared-storage-path",
                    description: "If the build is running across multiple machines (depending on the executor), this is the network share for Windows machines to access.");

                MacSharedStoragePath = new Option<string?>(
                    "--mac-shared-storage-path",
                    description: "If the build is running across multiple machines (depending on the executor), this is the local path on macOS pre-mounted to the network share.");
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
            private readonly GitLabBuildExecutorFactory _gitLabBuildExecutorFactory;

            public BuildCommandInstance(
                ILogger<BuildCommandInstance> logger,
                Options options,
                IBuildSpecificationGenerator buildSpecificationGenerator,
                LocalBuildExecutorFactory localBuildExecutorFactory,
                GitLabBuildExecutorFactory gitLabBuildExecutorFactory)
            {
                _logger = logger;
                _options = options;
                _buildSpecificationGenerator = buildSpecificationGenerator;
                _localBuildExecutorFactory = localBuildExecutorFactory;
                _gitLabBuildExecutorFactory = gitLabBuildExecutorFactory;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var engine = context.ParseResult.GetValueForOption(_options.Engine)!;
                var path = context.ParseResult.GetValueForOption(_options.Path)!;
                var distribution = context.ParseResult.GetValueForOption(_options.Distribution);
                var shipping = context.ParseResult.GetValueForOption(_options.Shipping);
                var executorName = context.ParseResult.GetValueForOption(_options.Executor);
                var executorOutputFile = context.ParseResult.GetValueForOption(_options.ExecutorOutputFile);
                var windowsSharedStoragePath = context.ParseResult.GetValueForOption(_options.WindowsSharedStoragePath);
                var macSharedStoragePath = context.ParseResult.GetValueForOption(_options.MacSharedStoragePath);

                // @todo: Move this validation to the parsing APIs.
                if (executorName == "local")
                {
                    if (string.IsNullOrWhiteSpace(windowsSharedStoragePath))
                    {
                        windowsSharedStoragePath = Path.Combine(path.DirectoryPath, ".SharedStorage");
                    }
                    if (string.IsNullOrWhiteSpace(macSharedStoragePath))
                    {
                        macSharedStoragePath = Path.Combine(path.DirectoryPath, ".SharedStorage");
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(windowsSharedStoragePath))
                    {
                        _logger.LogError("--windows-shared-storage-path must be set when not using the local executor.");
                        return 1;
                    }
                    if (string.IsNullOrWhiteSpace(macSharedStoragePath))
                    {
                        _logger.LogError("--mac-shared-storage-path must be set when not using the local executor.");
                        return 1;
                    }
                }

                _logger.LogInformation($"--engine:                      {engine}");
                _logger.LogInformation($"--path:                        {path}");
                _logger.LogInformation($"--distribution:                {(distribution == null ? "(not set)" : distribution)}");
                _logger.LogInformation($"--shipping:                    {(distribution != null ? "n/a" : (shipping ? "yes" : "no"))}");
                _logger.LogInformation($"--executor:                    {executorName}");
                _logger.LogInformation($"--executor-output-file:        {executorOutputFile}");
                _logger.LogInformation($"--windows-shared-storage-path: {windowsSharedStoragePath}");
                _logger.LogInformation($"--mac-shared-storage-path:     {macSharedStoragePath}");

                BuildEngineSpecification engineSpec;
                switch (engine.Type)
                {
                    case EngineSpecType.UEFSPackageTag:
                        engineSpec = BuildEngineSpecification.ForUEFSPackageTag(engine.UEFSPackageTag!);
                        break;
                    case EngineSpecType.Version:
                        engineSpec = BuildEngineSpecification.ForVersionWithPath(engine.Version!, engine.Path!);
                        break;
                    case EngineSpecType.Path:
                        engineSpec = BuildEngineSpecification.ForAbsolutePath(engine.Path!);
                        break;
                    default:
                        throw new NotSupportedException();
                }

                var buildGraphEnvironment = new Redpoint.UET.BuildPipeline.Environment.BuildGraphEnvironment
                {
                    // @todo: Make this not GitLab-dependent.
                    PipelineId = Environment.GetEnvironmentVariable("CI_PIPELINE_ID") ?? string.Empty,
                    Windows = new Redpoint.UET.BuildPipeline.Environment.BuildGraphWindowsEnvironment
                    {
                        SharedStorageAbsolutePath = $"{windowsSharedStoragePath.TrimEnd('\\')}\\",
                    },
                    Mac = new Redpoint.UET.BuildPipeline.Environment.BuildGraphMacEnvironment
                    {
                        SharedStorageAbsolutePath = $"{macSharedStoragePath.TrimEnd('/')}/",
                    },
                    // @note: Turned off until we can fix folder snapshotting in UEFS.
                    UseStorageVirtualisation = false,
                };

                BuildSpecification buildSpec;
                switch (path!.Type)
                {
                    case PathSpecType.BuildConfig:
                        switch (distribution!.Distribution)
                        {
                            case BuildConfigProjectDistribution projectDistribution:
                                buildSpec = _buildSpecificationGenerator.BuildConfigProjectToBuildSpec(
                                    engineSpec,
                                    buildGraphEnvironment,
                                    projectDistribution,
                                    repositoryRoot: path.DirectoryPath,
                                    executeBuild: true,
                                    strictIncludes: false,
                                    executeTests: false,
                                    executeDeployment: false);
                                break;
                            case BuildConfigPluginDistribution pluginDistribution:
                                buildSpec = _buildSpecificationGenerator.BuildConfigPluginToBuildSpec(
                                    engineSpec,
                                    pluginDistribution);
                                break;
                            case BuildConfigEngineDistribution engineDistribution:
                                buildSpec = _buildSpecificationGenerator.BuildConfigEngineToBuildSpec(
                                    engineSpec,
                                    engineDistribution);
                                break;
                            default:
                                throw new NotSupportedException();
                        }
                        break;
                    case PathSpecType.UProject:
                        buildSpec = _buildSpecificationGenerator.ProjectPathSpecToBuildSpec(
                            engineSpec,
                            buildGraphEnvironment,
                            path,
                            context.ParseResult.GetValueForOption(_options.Shipping));
                        break;
                    case PathSpecType.UPlugin:
                        buildSpec = _buildSpecificationGenerator.PluginPathSpecToBuildSpec(
                            engineSpec,
                            path);
                        break;
                    default:
                        throw new NotSupportedException();
                }

                IBuildExecutor executor;
                switch (executorName)
                {
                    case "local":
                        executor = _localBuildExecutorFactory.CreateExecutor();
                        break;
                    case "gitlab":
                        executor = _gitLabBuildExecutorFactory.CreateExecutor(executorOutputFile!);
                        break;
                    default:
                        throw new NotSupportedException();
                }

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
