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
    using Redpoint.UET.Configuration.Project;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Engine;
    using Redpoint.UET.BuildPipeline.Executors.Local;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.BuildPipeline.Executors.GitLab;
    using UET.Services;
    using System.CommandLine.Help;
    using Redpoint.UET.BuildPipeline.Executors.BuildServer;
    using Redpoint.UET.Core;

    internal class BuildCommand
    {
        internal class Options
        {
            public Option<EngineSpec> Engine;
            public Option<PathSpec> Path;
            public Option<bool> Test;
            public Option<bool> Deploy;
            public Option<bool> StrictIncludes;

            public Option<DistributionSpec?> Distribution;

            public Option<bool> Shipping;
            public Option<string[]> Platform;

            public Option<string> Executor;
            public Option<string> ExecutorOutputFile;
            public Option<string?> WindowsSharedStoragePath;
            public Option<string?> WindowsSdksPath;
            public Option<string?> MacSharedStoragePath;
            public Option<string?> MacSdksPath;

            public Options(
                IServiceProvider serviceProvider)
            {
                const string buildConfigOptions = "Options when targeting a BuildConfig.json file:";
                const string uprojectpluginOptions = "Options when targeting a .uplugin or .uproject file:";
                const string cicdOptions = "Options when building on CI/CD:";

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
                    parseArgument: DistributionSpec.ParseDistributionSpec(serviceProvider, Path),
                    isDefault: true);
                Distribution.AddAlias("-d");
                Distribution.Arity = ArgumentArity.ExactlyOne;
                Distribution.ArgumentGroupName = buildConfigOptions;

                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine to use for the build.",
                    parseArgument: EngineSpec.ParseEngineSpec(Path, Distribution),
                    isDefault: true);
                Engine.AddAlias("-e");
                Engine.Arity = ArgumentArity.ExactlyOne;

                Test = new Option<bool>(
                    "--test",
                    description: "If set, executes the tests after building.");

                Deploy = new Option<bool>(
                    "--deploy",
                    description: "If set, executes the deployment after building (and testing if --test is set).");

                StrictIncludes = new Option<bool>(
                    "--strict-includes",
                    description: "If set, disables unity and PCH builds. This forces all files to have the correct #include directives, at the cost of increased build time.");

                Shipping = new Option<bool>(
                    "--shipping",
                    description: "If set, builds for Shipping instead of Development.");
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
                Shipping.ArgumentGroupName = uprojectpluginOptions;

                Platform = new Option<string[]>(
                    "--platform",
                    description: "Add this platform to the build. You can pass this option multiple times to target many platforms. The host platform is always built.");
                Platform.ArgumentGroupName = uprojectpluginOptions;

                Executor = new Option<string>(
                    "--executor",
                    description: "The executor to use.",
                    getDefaultValue: () => "local");
                Executor.AddAlias("-x");
                Executor.FromAmong("local", "gitlab");
                Executor.ArgumentGroupName = cicdOptions;

                ExecutorOutputFile = new Option<string>(
                    "--executor-output-file",
                    description: "If the executor runs the build externally (e.g. a build server), this is the path to the emitted file that should be passed as the job or build description into the build server.");
                ExecutorOutputFile.ArgumentGroupName = cicdOptions;

                WindowsSharedStoragePath = new Option<string?>(
                    "--windows-shared-storage-path",
                    description: "If the build is running across multiple machines (depending on the executor), this is the network share for Windows machines to access.");
                WindowsSharedStoragePath.ArgumentGroupName = cicdOptions;

                WindowsSdksPath = new Option<string?>(
                    "--windows-sdks-path",
                    description: "The path that UET will automatically manage and install platform SDKs, and store them in the provided path on Windows machines. This should be a local path; the SDKs will be installed on each machine as they're needed.");
                WindowsSdksPath.ArgumentGroupName = cicdOptions;

                MacSharedStoragePath = new Option<string?>(
                    "--mac-shared-storage-path",
                    description: "If the build is running across multiple machines (depending on the executor), this is the local path on macOS pre-mounted to the network share.");
                MacSharedStoragePath.ArgumentGroupName = cicdOptions;

                MacSdksPath = new Option<string?>(
                    "--mac-sdks-path",
                    description: "The path that UET will automatically manage and install platform SDKs, and store them in the provided path on macOS machines. This should be a local path; the SDKs will be installed on each machine as they're needed.");
                MacSdksPath.ArgumentGroupName = cicdOptions;
            }
        }

        public static Command CreateBuildCommand()
        {
            var command = new Command("build", "Build an Unreal Engine project or plugin.");
            command.AddServicedOptionsHandler<BuildCommandInstance, Options>(
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
            private readonly IStringUtilities _stringUtilities;

            public BuildCommandInstance(
                ILogger<BuildCommandInstance> logger,
                Options options,
                IBuildSpecificationGenerator buildSpecificationGenerator,
                LocalBuildExecutorFactory localBuildExecutorFactory,
                GitLabBuildExecutorFactory gitLabBuildExecutorFactory,
                IStringUtilities stringUtilities)
            {
                _logger = logger;
                _options = options;
                _buildSpecificationGenerator = buildSpecificationGenerator;
                _localBuildExecutorFactory = localBuildExecutorFactory;
                _gitLabBuildExecutorFactory = gitLabBuildExecutorFactory;
                _stringUtilities = stringUtilities;
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
                var windowsSdksPath = context.ParseResult.GetValueForOption(_options.WindowsSdksPath);
                var macSharedStoragePath = context.ParseResult.GetValueForOption(_options.MacSharedStoragePath);
                var macSdksPath = context.ParseResult.GetValueForOption(_options.MacSdksPath);
                var test = context.ParseResult.GetValueForOption(_options.Test);
                var deploy = context.ParseResult.GetValueForOption(_options.Deploy);
                var strictIncludes = context.ParseResult.GetValueForOption(_options.StrictIncludes);
                var platforms = context.ParseResult.GetValueForOption(_options.Platform);

                // @todo: Move this validation to the parsing APIs.
                if (executorName == "local")
                {
                    if (string.IsNullOrWhiteSpace(windowsSharedStoragePath))
                    {
                        windowsSharedStoragePath = Path.Combine(path.DirectoryPath, ".uet", "shared-storage");
                    }
                    if (string.IsNullOrWhiteSpace(macSharedStoragePath))
                    {
                        macSharedStoragePath = Path.Combine(path.DirectoryPath, ".uet", "shared-storage");
                    }
                    if (string.IsNullOrWhiteSpace(windowsSdksPath))
                    {
                        windowsSdksPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                            "UET",
                            "SDKs");
                    }
                    if (string.IsNullOrWhiteSpace(macSdksPath))
                    {
                        macSdksPath = "/Users/Shared/UET/SDKs";
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
                _logger.LogInformation($"--windows-sdks-path:           {windowsSdksPath}");
                _logger.LogInformation($"--mac-shared-storage-path:     {macSharedStoragePath}");
                _logger.LogInformation($"--mac-sdks-path:               {macSdksPath}");
                _logger.LogInformation($"--test:                        {(test ? "yes" : "no")}");
                _logger.LogInformation($"--deploy:                      {(deploy ? "yes" : "no")}");
                _logger.LogInformation($"--strict-includes:             {(strictIncludes ? "yes" : "no")}");

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

                // @note: We need the build executor to get the pipeline ID, which is also used as an input to compute the derived storage path that's specific for this build.
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

                // Compute the shared storage name for this build.
                var pipelineId = executor.DiscoverPipelineId();
                var sharedStorageName = _stringUtilities.GetStabilityHash(
                    $"{pipelineId}-{distribution?.DistributionCanonicalName}-{engineSpec.ToReparsableString()}",
                    null);
                _logger.LogInformation($"Using pipeline ID: {pipelineId}");
                _logger.LogInformation($"Using shared storage name: {sharedStorageName}");

                // Derive the shared storage paths.
                windowsSharedStoragePath = $"{windowsSharedStoragePath.TrimEnd('\\')}\\{sharedStorageName}\\";
                macSharedStoragePath = $"{macSharedStoragePath.TrimEnd('/')}/{sharedStorageName}/";
                _logger.LogInformation($"Derived shared storage path for Windows: {windowsSharedStoragePath}");
                _logger.LogInformation($"Derived shared storage path for macOS: {macSharedStoragePath}");

                var buildGraphEnvironment = new Redpoint.UET.BuildPipeline.Environment.BuildGraphEnvironment
                {
                    PipelineId = pipelineId,
                    Windows = new Redpoint.UET.BuildPipeline.Environment.BuildGraphWindowsEnvironment
                    {
                        SharedStorageAbsolutePath = windowsSharedStoragePath,
                        SdksPath = windowsSdksPath?.TrimEnd('\\'),
                    },
                    Mac = new Redpoint.UET.BuildPipeline.Environment.BuildGraphMacEnvironment
                    {
                        SharedStorageAbsolutePath = macSharedStoragePath,
                        SdksPath = macSdksPath?.TrimEnd('/'),
                    },
                    // @note: Turned off until we can fix folder snapshotting in UEFS.
                    UseStorageVirtualisation = false,
                };

                BuildSpecification buildSpec;
                try
                {
                    switch (path!.Type)
                    {
                        case PathSpecType.BuildConfig:
                            switch (distribution!.Distribution)
                            {
                                case BuildConfigProjectDistribution projectDistribution:
                                    buildSpec = await _buildSpecificationGenerator.BuildConfigProjectToBuildSpecAsync(
                                        engineSpec,
                                        buildGraphEnvironment,
                                        projectDistribution,
                                        repositoryRoot: path.DirectoryPath,
                                        executeBuild: true,
                                        executeTests: test,
                                        executeDeployment: deploy,
                                        strictIncludes: strictIncludes,
                                        localExecutor: executorName == "local");
                                    break;
                                case BuildConfigPluginDistribution pluginDistribution:
                                    buildSpec = await _buildSpecificationGenerator.BuildConfigPluginToBuildSpecAsync(
                                        engineSpec,
                                        buildGraphEnvironment,
                                        pluginDistribution,
                                        (BuildConfigPlugin)distribution.BuildConfig,
                                        repositoryRoot: path.DirectoryPath,
                                        executeBuild: true,
                                        executePackage: true,
                                        executeTests: test,
                                        executeDeployment: deploy,
                                        strictIncludes: strictIncludes,
                                        localExecutor: executorName == "local");
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
                                shipping,
                                strictIncludes,
                                platforms ?? new string[0]);
                            break;
                        case PathSpecType.UPlugin:
                            buildSpec = await _buildSpecificationGenerator.PluginPathSpecToBuildSpecAsync(
                                engineSpec,
                                buildGraphEnvironment,
                                path,
                                shipping,
                                strictIncludes,
                                platforms ?? new string[0]);
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }
                catch (BuildMisconfigurationException ex)
                {
                    _logger.LogError(ex.Message);
                    return 1;
                }

                try
                {
                    var executionEvents = new LoggerBasedBuildExecutionEvents(_logger);
                    var buildResult = await executor.ExecuteBuildAsync(
                        buildSpec,
                        executionEvents,
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                    if (buildResult == 0)
                    {
                        _logger.LogInformation("All build jobs \u001b[32mpassed successfully\u001b[0m.");
                    }
                    else
                    {
                        _logger.LogError("One or more build jobs \u001b[31mfailed\u001b[0m:");
                        foreach (var kv in executionEvents.GetResults())
                        {
                            switch (kv.resultStatus)
                            {
                                case BuildResultStatus.Success:
                                    _logger.LogInformation($"{kv.nodeName} = \x001B[32mPassed\x001B[0m");
                                    break;
                                case BuildResultStatus.Failed:
                                    _logger.LogInformation($"{kv.nodeName} = \x001B[31mFailed\x001B[0m");
                                    break;
                                case BuildResultStatus.Cancelled:
                                    _logger.LogInformation($"{kv.nodeName} = \x001B[33mCancelled\x001B[0m");
                                    break;
                                case BuildResultStatus.NotRun:
                                    _logger.LogInformation($"{kv.nodeName} = \x001B[36mNot Run\x001B[0m");
                                    break;
                            }
                        }
                    }
                    return buildResult;
                }
                catch (BuildPipelineExecutionFailure ex)
                {
                    _logger.LogError(ex.Message);
                    return 1;
                }
            }
        }
    }
}
