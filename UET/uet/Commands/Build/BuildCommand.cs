namespace UET.Commands.Build
{
    using System;
    using System.CommandLine;
    using System.Threading.Tasks;
    using UET.Commands.EngineSpec;
    using System.CommandLine.Invocation;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Engine;
    using Redpoint.Uet.BuildPipeline.Executors.Local;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildPipeline.Executors.GitLab;
    using Redpoint.Uet.Core;
    using static Crayon.Output;
    using Redpoint.Uet.Configuration.Dynamic;
    using System.Text.RegularExpressions;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.CommonPaths;

    internal sealed class BuildCommand
    {
        internal sealed class Options
        {
            public Option<EngineSpec> Engine;
            public Option<PathSpec> Path;
            public Option<bool> Test;
            public Option<bool> Deploy;
            public Option<bool> StrictIncludes;
            public Option<bool> StorageVirtualisation;

            public Option<DistributionSpec?> Distribution;

            public Option<bool> Shipping;
            public Option<string[]> Platform;
            public Option<string> PluginPackage;

            public Option<string?> PluginVersionName;
            public Option<long?> PluginVersionNumber;

            public Option<string> ProjectStagingDirectory;

            public Option<string> Executor;
            public Option<string> ExecutorOutputFile;
            public Option<string?> WindowsSharedStoragePath;
            public Option<string?> WindowsSharedGitCachePath;
            public Option<string?> WindowsSdksPath;
            public Option<string?> MacSharedStoragePath;
            public Option<string?> MacSharedGitCachePath;
            public Option<string?> MacSdksPath;

            public Options(
                IServiceProvider serviceProvider)
            {
                const string buildConfigOptions = "Options when targeting a BuildConfig.json file:";
                const string uprojectpluginOptions = "Options when targeting a .uplugin or .uproject file:";
                const string pluginOptions = "Options when building a plugin:";
                const string projectOptions = "Options when building a project:";
                const string cicdOptions = "Options when building on CI/CD:";
                const string cicdEngineOptions = "Options when building the engine on CI/CD:";

                // ==== General options

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

                StorageVirtualisation = new Option<bool>(
                    "--storage-virtualisation",
                    getDefaultValue: () => Environment.GetEnvironmentVariable("UET_USE_STORAGE_VIRTUALIZATION") == "true",
                    description: "If set, enables storage virtualisation via UEFS.");
                StorageVirtualisation.AddAlias("-u");

                // ==== .uproject / .uplugin options

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
                    description: "Add this platform to the build. You can pass this option multiple times to target many platforms. The host platform is always built.")
                {
                    ArgumentGroupName = uprojectpluginOptions
                };

                PluginPackage = new Option<string>(
                    "--plugin-package",
                    description: "When building a .uplugin file, specifies if and how the plugin should be packaged (defaults to 'none'). When building from a BuildConfig.json file, it can be explicitly set to 'none' to turn off the plugin packaging steps (values other than 'none' are not permitted; the BuildConfig.json file controls how the plugin is packaged).");
                PluginPackage.FromAmong("none", "generic", "marketplace", "fab");
                PluginPackage.ArgumentGroupName = uprojectpluginOptions;

                // ==== Plugin options, regardless of build type

                PluginVersionName = new Option<string?>(
                    "--plugin-version-name",
                    description:
                        """
                        Set the plugin package to use this version name instead of the auto-generated default.
                        If this option is not provided, and you are not building on a CI server, the version will be set to 'Unversioned'.
                        If this option is not provided, and you are building on a CI server, UET will use the format, generating versions such as '2023.12.30-5.2-1aeb4233'.
                        If you are building on a CI server and only want to override the date component of the auto-generated version, you can set the 'OVERRIDE_DATE_VERSION' environment variable instead of using this option.
                        """)
                {
                    ArgumentGroupName = pluginOptions
                };

                PluginVersionNumber = new Option<long?>(
                    "--plugin-version-number",
                    description:
                        """
                        Set the plugin package to use this version number instead of the auto-generated default.
                        If this option is not provided, and you are not building on a CI server, the version number will be set to 10000.
                        If this option is not provided, and you are building on a CI server, UET will compute a version number from the UNIX timestamp and engine version number.
                        """)
                {
                    ArgumentGroupName = pluginOptions
                };

                // ==== Project options, regardless of build type

                ProjectStagingDirectory = new Option<string>(
                    "--project-staging-directory",
                    description: "When building a project, either as a .uproject or via BuildConfig.json, overrides the path that project builds are staged to. The default is __REPOSITORY_ROOT__/Saved/StagedBuilds which places builds underneath the 'Saved/StagedBuilds' folder in the project. You can use absolute paths here and you can use __REPOSITORY_ROOT__ to refer to the project folder.")
                {
                    ArgumentGroupName = projectOptions
                };

                // ==== CI/CD options

                Executor = new Option<string>(
                    "--executor",
                    description: "The executor to use.",
                    getDefaultValue: () => "local");
                Executor.AddAlias("-x");
                Executor.FromAmong("local", "gitlab");
                Executor.ArgumentGroupName = cicdOptions;

                ExecutorOutputFile = new Option<string>(
                    "--executor-output-file",
                    description: "If the executor runs the build externally (e.g. a build server), this is the path to the emitted file that should be passed as the job or build description into the build server.")
                {
                    ArgumentGroupName = cicdOptions
                };

                WindowsSharedStoragePath = new Option<string?>(
                    "--windows-shared-storage-path",
                    description: "If the build is running across multiple machines (depending on the executor), this is the network share for Windows machines to access.")
                {
                    ArgumentGroupName = cicdOptions
                };

                WindowsSdksPath = new Option<string?>(
                    "--windows-sdks-path",
                    description: "The path that UET will automatically manage and install platform SDKs, and store them in the provided path on Windows machines. This should be a local path; the SDKs will be installed on each machine as they're needed.")
                {
                    ArgumentGroupName = cicdOptions
                };

                MacSharedStoragePath = new Option<string?>(
                    "--mac-shared-storage-path",
                    description: "If the build is running across multiple machines (depending on the executor), this is the local path on macOS pre-mounted to the network share.")
                {
                    ArgumentGroupName = cicdOptions
                };

                MacSdksPath = new Option<string?>(
                    "--mac-sdks-path",
                    description: "The path that UET will automatically manage and install platform SDKs, and store them in the provided path on macOS machines. This should be a local path; the SDKs will be installed on each machine as they're needed.")
                {
                    ArgumentGroupName = cicdOptions
                };

                WindowsSharedGitCachePath = new Option<string?>(
                    "--windows-shared-git-cache-path",
                    description: "If the build is running across multiple machines (depending on the executor), this is the network share where Git commits and Git dependencies are cached, so that they don't need to be re-downloaded on each machine. If not specified, each machine will download their own copy of the commits and Git dependencies.")
                {
                    ArgumentGroupName = cicdEngineOptions
                };

                MacSharedGitCachePath = new Option<string?>(
                    "--mac-shared-git-cache-path",
                    description: "If the build is running across multiple machines (depending on the executor), this is the local path on macOS pre-mounted to the network share where Git commits and Git dependencies are cached, so that they don't need to be re-downloaded on each machine. If not specified, each machine will download their own copy of the commits and Git dependencies.")
                {
                    ArgumentGroupName = cicdEngineOptions
                };
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

        private sealed class BuildCommandInstance : ICommandInstance
        {
            private readonly ILogger<BuildCommandInstance> _logger;
            private readonly Options _options;
            private readonly IBuildSpecificationGenerator _buildSpecificationGenerator;
            private readonly LocalBuildExecutorFactory _localBuildExecutorFactory;
            private readonly GitLabBuildExecutorFactory _gitLabBuildExecutorFactory;
            private readonly IStringUtilities _stringUtilities;
            private readonly IDynamicWorkspaceProvider _dynamicWorkspaceProvider;

            public BuildCommandInstance(
                ILogger<BuildCommandInstance> logger,
                Options options,
                IBuildSpecificationGenerator buildSpecificationGenerator,
                LocalBuildExecutorFactory localBuildExecutorFactory,
                GitLabBuildExecutorFactory gitLabBuildExecutorFactory,
                IStringUtilities stringUtilities,
                IDynamicWorkspaceProvider dynamicWorkspaceProvider)
            {
                _logger = logger;
                _options = options;
                _buildSpecificationGenerator = buildSpecificationGenerator;
                _localBuildExecutorFactory = localBuildExecutorFactory;
                _gitLabBuildExecutorFactory = gitLabBuildExecutorFactory;
                _stringUtilities = stringUtilities;
                _dynamicWorkspaceProvider = dynamicWorkspaceProvider;
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
                var windowsSharedGitCachePath = context.ParseResult.GetValueForOption(_options.WindowsSharedGitCachePath);
                var windowsSdksPath = context.ParseResult.GetValueForOption(_options.WindowsSdksPath);
                var macSharedStoragePath = context.ParseResult.GetValueForOption(_options.MacSharedStoragePath);
                var macSharedGitCachePath = context.ParseResult.GetValueForOption(_options.MacSharedGitCachePath);
                var macSdksPath = context.ParseResult.GetValueForOption(_options.MacSdksPath);
                var test = context.ParseResult.GetValueForOption(_options.Test);
                var deploy = context.ParseResult.GetValueForOption(_options.Deploy);
                var strictIncludes = context.ParseResult.GetValueForOption(_options.StrictIncludes);
                var storageVirtualisation = context.ParseResult.GetValueForOption(_options.StorageVirtualisation);
                var platforms = context.ParseResult.GetValueForOption(_options.Platform);
                var projectStagingDirectory = context.ParseResult.GetValueForOption(_options.ProjectStagingDirectory);
                var pluginPackage = context.ParseResult.GetValueForOption(_options.PluginPackage);
                var pluginVersionName = context.ParseResult.GetValueForOption(_options.PluginVersionName);
                var pluginVersionNumber = context.ParseResult.GetValueForOption(_options.PluginVersionNumber);

                // Configure the dynamic workspace provider to use workspace virtualisation
                // if appropriate.
                _dynamicWorkspaceProvider.UseWorkspaceVirtualisation = storageVirtualisation;

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
                        windowsSdksPath = UetPaths.UetDefaultWindowsSdkStoragePath;
                    }
                    if (string.IsNullOrWhiteSpace(macSdksPath))
                    {
                        macSdksPath = UetPaths.UetDefaultMacSdkStoragePath;
                    }
                }
                else
                {
                    // Inherit from environment variables if things aren't specified on the command line.
                    var windowsSharedStoragePathEnv = Environment.GetEnvironmentVariable("UET_WINDOWS_SHARED_STORAGE_PATH");
                    var windowsSdksPathEnv = Environment.GetEnvironmentVariable("UET_WINDOWS_SDKS_PATH");
                    var macSharedStoragePathEnv = Environment.GetEnvironmentVariable("UET_MAC_SHARED_STORAGE_PATH");
                    var macSdksPathEnv = Environment.GetEnvironmentVariable("UET_MAC_SDKS_PATH");
                    if (string.IsNullOrWhiteSpace(windowsSharedStoragePath))
                    {
                        windowsSharedStoragePath = windowsSharedStoragePathEnv;
                    }
                    if (string.IsNullOrWhiteSpace(windowsSdksPath))
                    {
                        windowsSdksPath = windowsSdksPathEnv;
                    }
                    if (string.IsNullOrWhiteSpace(macSharedStoragePath))
                    {
                        macSharedStoragePath = macSharedStoragePathEnv;
                    }
                    if (string.IsNullOrWhiteSpace(macSdksPath))
                    {
                        macSdksPath = macSdksPathEnv;
                    }

                    // Ensure that at least the shared storage paths are set.
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

                _logger.LogInformation($"--engine:                        {engine}");
                _logger.LogInformation($"--path:                          {path}");
                _logger.LogInformation($"--distribution:                  {(distribution == null ? "(not set)" : distribution)}");
                _logger.LogInformation($"--shipping:                      {(distribution != null ? "n/a" : (shipping ? "yes" : "no"))}");
                _logger.LogInformation($"--executor:                      {executorName}");
                _logger.LogInformation($"--executor-output-file:          {executorOutputFile}");
                _logger.LogInformation($"--windows-shared-storage-path:   {windowsSharedStoragePath}");
                _logger.LogInformation($"--windows-shared-git-cache-path: {windowsSharedGitCachePath}");
                _logger.LogInformation($"--windows-sdks-path:             {windowsSdksPath}");
                _logger.LogInformation($"--mac-shared-storage-path:       {macSharedStoragePath}");
                _logger.LogInformation($"--mac-shared-git-cache-path:     {macSharedGitCachePath}");
                _logger.LogInformation($"--mac-sdks-path:                 {macSdksPath}");
                _logger.LogInformation($"--test:                          {(test ? "yes" : "no")}");
                _logger.LogInformation($"--deploy:                        {(deploy ? "yes" : "no")}");
                _logger.LogInformation($"--strict-includes:               {(strictIncludes ? "yes" : "no")}");
                _logger.LogInformation($"--storage-virtualisation:        {(storageVirtualisation ? "yes" : "no")}");
                _logger.LogInformation($"--platforms:                     {string.Join(", ", platforms ?? [])}");
                _logger.LogInformation($"--plugin-package:                {pluginPackage}");
                _logger.LogInformation($"--plugin-version-name:           {pluginVersionName}");
                _logger.LogInformation($"--plugin-version-number:         {pluginVersionNumber}");

                var engineSpec = engine.ToBuildEngineSpecification(
                    "build",
                    distribution,
                    windowsSharedGitCachePath,
                    macSharedGitCachePath);

                // @note: We need the build executor to get the pipeline ID, which is also used as an input to compute the derived storage path that's specific for this build.
                var executor = executorName switch
                {
                    "local" => _localBuildExecutorFactory.CreateExecutor(),
                    "gitlab" => _gitLabBuildExecutorFactory.CreateExecutor(executorOutputFile!),
                    _ => throw new NotSupportedException(),
                };

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

                var buildGraphEnvironment = new Redpoint.Uet.BuildPipeline.Environment.BuildGraphEnvironment
                {
                    PipelineId = pipelineId,
                    Windows = new Redpoint.Uet.BuildPipeline.Environment.BuildGraphWindowsEnvironment
                    {
                        SharedStorageAbsolutePath = windowsSharedStoragePath,
                        SdksPath = windowsSdksPath?.TrimEnd('\\'),
                    },
                    Mac = new Redpoint.Uet.BuildPipeline.Environment.BuildGraphMacEnvironment
                    {
                        SharedStorageAbsolutePath = macSharedStoragePath,
                        SdksPath = macSdksPath?.TrimEnd('/'),
                    },
                    UseStorageVirtualisation = storageVirtualisation,
                };

                BuildSpecification buildSpec;
                BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>[]? preparePlugin = null;
                BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>[]? prepareProject = null;
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
                                        (BuildConfigProject)distribution.BuildConfig,
                                        projectDistribution,
                                        repositoryRoot: path.DirectoryPath,
                                        executeBuild: true,
                                        executeTests: test,
                                        executeDeployment: deploy,
                                        strictIncludes: strictIncludes,
                                        localExecutor: executorName == "local",
                                        alternateStagingDirectory: projectStagingDirectory).ConfigureAwait(false);
                                    prepareProject = projectDistribution.Prepare;
                                    break;
                                case BuildConfigPluginDistribution pluginDistribution:
                                    if (pluginPackage is not null and not "none")
                                    {
                                        _logger.LogError("The --plugin-package option can not be used when building using a BuildConfig.json file (unless it is set to 'none'), as the BuildConfig.json file controls how the plugin will be packaged instead.");
                                        return 1;
                                    }
                                    if (pluginPackage == "none" && (test || deploy))
                                    {
                                        _logger.LogError("The --plugin-package option can not be set to 'none' while also passing --test or --deploy (as plugin packaging is required for those steps), when building using a BuildConfig.json file. Either remove --test and --deploy or remove --plugin-package 'none' from the command line.");
                                        return 1;
                                    }
                                    buildSpec = await _buildSpecificationGenerator.BuildConfigPluginToBuildSpecAsync(
                                        engineSpec,
                                        buildGraphEnvironment,
                                        (BuildConfigPlugin)distribution.BuildConfig,
                                        pluginDistribution,
                                        repositoryRoot: path.DirectoryPath,
                                        executeBuild: true,
                                        executePackage: pluginPackage != "none",
                                        executeZip: pluginPackage != "none",
                                        executeTests: pluginPackage != "none" && test,
                                        executeDeployment: pluginPackage != "none" && deploy,
                                        strictIncludes: strictIncludes,
                                        localExecutor: executorName == "local",
                                        isPluginRooted: false,
                                        commandlinePluginVersionName: pluginVersionName,
                                        commandlinePluginVersionNumber: pluginVersionNumber).ConfigureAwait(false);
                                    preparePlugin = pluginDistribution.Prepare;
                                    break;
                                case BuildConfigEngineDistribution engineDistribution:
                                    buildSpec = await _buildSpecificationGenerator.BuildConfigEngineToBuildSpecAsync(
                                        engineSpec,
                                        buildGraphEnvironment,
                                        engineDistribution,
                                        context.GetCancellationToken()).ConfigureAwait(false);
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
                                platforms ?? [],
                                projectStagingDirectory);
                            break;
                        case PathSpecType.UPlugin:
                            buildSpec = await _buildSpecificationGenerator.PluginPathSpecToBuildSpecAsync(
                                engineSpec,
                                buildGraphEnvironment,
                                path,
                                shipping,
                                strictIncludes,
                                platforms ?? [],
                                package: (pluginPackage ?? "none") != "none",
                                packageType: (pluginPackage ?? "none") switch
                                {
                                    "none" => BuildConfigPluginPackageType.Generic,
                                    "generic" => BuildConfigPluginPackageType.Generic,
                                    "marketplace" => BuildConfigPluginPackageType.Marketplace,
                                    "fab" => BuildConfigPluginPackageType.Fab,
                                    _ => throw new NotSupportedException("pluginPackage value is not supported!")
                                },
                                commandlinePluginVersionName: pluginVersionName,
                                commandlinePluginVersionNumber: pluginVersionNumber).ConfigureAwait(false);
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
                        preparePlugin,
                        prepareProject,
                        executionEvents,
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken()).ConfigureAwait(false);
                    if (buildResult == 0)
                    {
                        _logger.LogInformation($"All build jobs {Bright.Green("passed successfully")}.");
                    }
                    else
                    {
                        _logger.LogError($"One or more build jobs {Bright.Red("failed")}:");
                        foreach (var (nodeName, resultStatus) in executionEvents.GetResults())
                        {
                            switch (resultStatus)
                            {
                                case BuildResultStatus.Success:
                                    _logger.LogInformation($"{nodeName} = {Bright.Green("Passed")}");
                                    break;
                                case BuildResultStatus.Failed:
                                    _logger.LogInformation($"{nodeName} = {Bright.Red("Failed")}");
                                    break;
                                case BuildResultStatus.Cancelled:
                                    _logger.LogInformation($"{nodeName} = {Bright.Yellow("Cancelled")}");
                                    break;
                                case BuildResultStatus.NotRun:
                                    _logger.LogInformation($"{nodeName} = {Bright.Cyan("Not Run")}");
                                    break;
                            }
                        }
                    }
                    return buildResult;
                }
                catch (BuildPipelineExecutionFailureException ex)
                {
                    _logger.LogError(ex.Message);
                    return 1;
                }
            }
        }
    }
}
