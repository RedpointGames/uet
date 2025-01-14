namespace UET.Commands.Test
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.Local;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Automation;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Automation;
    using Redpoint.Uet.CommonPaths;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Core;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using UET.BuildConfig;
    using UET.Commands.Build;
    using UET.Commands.EngineSpec;
    using static Crayon.Output;

    internal sealed class TestCommand
    {
        internal sealed class Options
        {
            public Option<EngineSpec> Engine;
            public Option<PathSpec> Path;

            public Option<string> Prefix;
            public Option<string> Name;

            public Options()
            {
                Path = new Option<PathSpec>(
                    "--path",
                    description: "The directory path that contains a .uproject file, .uplugin file or BuildConfig.json file.",
                    parseArgument: result => PathSpec.ParsePathSpec(result, false),
                    isDefault: true);
                Path.AddAlias("-p");
                Path.Arity = ArgumentArity.ExactlyOne;

                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine to use for the build.",
                    parseArgument: EngineSpec.ParseEngineSpec(Path, null),
                    isDefault: true);
                Engine.AddAlias("-e");
                Engine.Arity = ArgumentArity.ExactlyOne;

                Prefix = new Option<string>(
                    "--prefix",
                    description: "The prefix for tests to run. If not set, defaults to either '<project name>.' or '<plugin name>.'.");

                Name = new Option<string>(
                    "--name",
                    description: "When testing against a BuildConfig.json file, the name or short name of the predefined test to run.");
                Name.AddAlias("-n");
            }
        }

        public static Command CreateTestCommand()
        {
            var command = new Command("test", "Run tests in the editor for a project or plugin.")
            {
                FullDescription = """
                This command runs tests for either an Unreal Engine project or plugin. The behaviour of this command depends on the arguments that you've passed to it and the current directory.

                -------------

                If this command is run in a directory with a .uproject or .uplugin file (but no BuildConfig.json file), this command runs automation tests against the Unreal Engine project or plugin using the editor binary for the current platform.

                If you don't specify --prefix, all automation tests are run. This is usually a lot more tests than you want, so you should specify --prefix in most cases.

                -------------

                If this command is run in a directory with a BuildConfig.json file, where that BuildConfig.json file describes a plugin, you must specify --name.

                The specified --name must match a test predefined in the 'Tests' section (outside of 'Distributions').
                """
            };
            command.AddServicedOptionsHandler<TestCommandInstance, Options>(
                services =>
                {
                    services.AddSingleton<IBuildSpecificationGenerator, DefaultBuildSpecificationGenerator>();
                });
            return command;
        }

        private sealed class TestCommandInstance : ICommandInstance
        {
            private readonly ILogger<TestCommandInstance> _logger;
            private readonly Options _options;
            private readonly IBuildSpecificationGenerator _buildSpecificationGenerator;
            private readonly LocalBuildExecutorFactory _localBuildExecutorFactory;
            private readonly IStringUtilities _stringUtilities;
            private readonly IServiceProvider _serviceProvider;

            public TestCommandInstance(
                ILogger<TestCommandInstance> logger,
                Options options,
                IBuildSpecificationGenerator buildSpecificationGenerator,
                LocalBuildExecutorFactory localBuildExecutorFactory,
                IStringUtilities stringUtilities,
                IServiceProvider serviceProvider)
            {
                _logger = logger;
                _options = options;
                _buildSpecificationGenerator = buildSpecificationGenerator;
                _localBuildExecutorFactory = localBuildExecutorFactory;
                _stringUtilities = stringUtilities;
                _serviceProvider = serviceProvider;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var engine = context.ParseResult.GetValueForOption(_options.Engine)!;
                var path = context.ParseResult.GetValueForOption(_options.Path)!;
                var prefix = context.ParseResult.GetValueForOption(_options.Prefix);
                var name = context.ParseResult.GetValueForOption(_options.Name);

                // @todo: Should we surface these options?
                var windowsSharedStoragePath = Path.Combine(path.DirectoryPath, ".uet", "shared-storage");
                var macSharedStoragePath = Path.Combine(path.DirectoryPath, ".uet", "shared-storage");
                var windowsSdksPath = UetPaths.UetDefaultWindowsSdkStoragePath;
                var macSdksPath = UetPaths.UetDefaultMacSdkStoragePath;

                if (string.IsNullOrWhiteSpace(prefix))
                {
                    if (path.UProjectPath != null)
                    {
                        prefix = Path.GetFileNameWithoutExtension(path.UProjectPath) + ".";
                    }
                    else if (path.UPluginPath != null)
                    {
                        prefix = Path.GetFileNameWithoutExtension(path.UPluginPath) + ".";
                    }
                }

                _logger.LogInformation($"--engine:                      {engine}");
                _logger.LogInformation($"--path:                        {path}");
                _logger.LogInformation($"--prefix:                      {prefix}");
                _logger.LogInformation($"--name:                        {name}");

                var engineSpec = engine.ToBuildEngineSpecification("test");

                // @note: We always use the local executor for this test command.
                var executor = _localBuildExecutorFactory.CreateExecutor();

                // Compute the shared storage name for this build.
                var pipelineId = executor.DiscoverPipelineId();
                var sharedStorageName = _stringUtilities.GetStabilityHash(
                    $"{pipelineId}--{engineSpec.ToReparsableString()}",
                    null);
                _logger.LogInformation($"Using pipeline ID: {pipelineId}");
                _logger.LogInformation($"Using shared storage name: {sharedStorageName}");

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
                    UseStorageVirtualisation = false,
                };

                BuildSpecification buildSpec;
                try
                {
                    switch (path!.Type)
                    {
                        case PathSpecType.UProject:
                            {
                                // Use heuristics to guess the targets for this build.
                                string editorTarget;
                                string gameTarget;
                                if (Directory.Exists(Path.Combine(path.DirectoryPath, "Source")))
                                {
                                    var files = Directory.GetFiles(Path.Combine(path.DirectoryPath, "Source"), "*.Target.cs");
                                    editorTarget = files.Where(x => x.EndsWith("Editor.Target.cs", StringComparison.Ordinal)).Select(x => Path.GetFileName(x)).First();
                                    editorTarget = editorTarget[..editorTarget.LastIndexOf(".Target.cs", StringComparison.Ordinal)];
                                    gameTarget = editorTarget[..editorTarget.LastIndexOf("Editor", StringComparison.Ordinal)];
                                }
                                else
                                {
                                    editorTarget = "UnrealEditor";
                                    gameTarget = "UnrealGame";
                                }

                                var buildConfigProject = new BuildConfigProject
                                {
                                    Type = BuildConfigType.Plugin,
                                    Distributions = new List<BuildConfigProjectDistribution>
                                    {
                                        new BuildConfigProjectDistribution
                                        {
                                            Name = "Test",
                                            ProjectName = Path.GetFileNameWithoutExtension(path.UProjectPath)!,
                                            Build = new BuildConfigProjectBuild
                                            {
                                                Editor = new BuildConfigProjectBuildEditor
                                                {
                                                    Target = editorTarget,
                                                }
                                            },
                                            Tests = new[]
                                            {
                                                new BuildConfigDynamic<BuildConfigProjectDistribution, ITestProvider>
                                                {
                                                    Type = "Automation",
                                                    Name = "Test",
                                                    Manual = false,
                                                    DynamicSettings = new BuildConfigProjectTestAutomation
                                                    {
                                                        TestPrefix = prefix ?? string.Empty,
                                                        TargetName = editorTarget,
                                                    }
                                                }
                                            }
                                        }
                                    }
                                };
                                buildSpec = await _buildSpecificationGenerator.BuildConfigProjectToBuildSpecAsync(
                                    engineSpec,
                                    buildGraphEnvironment,
                                    buildConfigProject,
                                    buildConfigProject.Distributions[0],
                                    repositoryRoot: path.DirectoryPath,
                                    executeBuild: true,
                                    executeTests: true,
                                    executeDeployment: false,
                                    strictIncludes: false,
                                    localExecutor: true,
                                    alternateStagingDirectory: null).ConfigureAwait(false);
                                break;
                            }
                        case PathSpecType.UPlugin:
                            {
                                var buildConfigPlugin = new BuildConfigPlugin
                                {
                                    PluginName = Path.GetFileNameWithoutExtension(path.UPluginPath!),
                                    Type = BuildConfigType.Plugin,
                                    Distributions = new List<BuildConfigPluginDistribution>
                                    {
                                        new BuildConfigPluginDistribution
                                        {
                                            Name = "Test",
                                            Build = new BuildConfigPluginBuild
                                            {
                                                Editor = new BuildConfigPluginBuildEditor
                                                {
                                                    Platforms = new[]
                                                    {
                                                        OperatingSystem.IsWindows()
                                                            ? BuildConfigPluginBuildEditorPlatform.Win64
                                                            : BuildConfigPluginBuildEditorPlatform.Mac
                                                    },
                                                }
                                            },
                                            Tests = new[]
                                            {
                                                new BuildConfigDynamic<BuildConfigPluginDistribution, ITestProvider>
                                                {
                                                    Type = "Automation",
                                                    Name = "Test",
                                                    Manual = false,
                                                    DynamicSettings = new BuildConfigPluginTestAutomation
                                                    {
                                                        Platforms = new[]
                                                        {
                                                            OperatingSystem.IsWindows()
                                                                ? BuildConfigHostPlatform.Win64
                                                                : BuildConfigHostPlatform.Mac
                                                        },
                                                        TestPrefix = prefix ?? string.Empty,
                                                    }
                                                }
                                            }
                                        }
                                    }
                                };
                                buildSpec = await _buildSpecificationGenerator.BuildConfigPluginToBuildSpecAsync(
                                    engineSpec,
                                    buildGraphEnvironment,
                                    buildConfigPlugin,
                                    buildConfigPlugin.Distributions[0],
                                    repositoryRoot: path.DirectoryPath,
                                    executeBuild: true,
                                    executeTests: true,
                                    executeDeployment: false,
                                    strictIncludes: false,
                                    localExecutor: true,
                                    isPluginRooted: true,
                                    commandlinePluginVersionName: null,
                                    commandlinePluginVersionNumber: null,
                                    skipPackaging: false).ConfigureAwait(false);
                                break;
                            }
                        case PathSpecType.BuildConfig:
                            {
                                var loadResult = BuildConfigLoader.TryLoad(
                                    _serviceProvider,
                                    Path.Combine(path.DirectoryPath, "BuildConfig.json"));
                                if (!loadResult.Success)
                                {
                                    _logger.LogError(string.Join("\n", loadResult.ErrorList));
                                    return 1;
                                }
                                if (loadResult.BuildConfig is BuildConfigPlugin buildConfigPlugin &&
                                    buildConfigPlugin.Tests != null)
                                {
                                    var test = buildConfigPlugin.Tests.FirstOrDefault(
                                        t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                                            || (t.ShortName != null && t.ShortName.Equals(name, StringComparison.OrdinalIgnoreCase)));
                                    if (test == null)
                                    {
                                        _logger.LogError($"This BuildConfig.json does not have a predefined test with a name or short name that matches '{name}'.");
                                        return 1;
                                    }

                                    // Now we make a synthetic BuildConfig.json file with a synthetic distribution that
                                    // specifies the dependencies correctly.
                                    var syntheticBuildConfigPlugin = new BuildConfigPlugin
                                    {
                                        PluginName = buildConfigPlugin.PluginName,
                                        Type = BuildConfigType.Plugin,
                                        Copyright = buildConfigPlugin.Copyright,
                                        Distributions = new List<BuildConfigPluginDistribution>
                                        {
                                            new BuildConfigPluginDistribution
                                            {
                                                Name = "Test",
                                                EnvironmentVariables = test.Dependencies?.EnvironmentVariables,
                                                Package = test.Dependencies?.Package ?? new BuildConfigPluginPackage
                                                {
                                                },
                                                Build = test.Dependencies?.Build ?? new BuildConfigPluginBuild
                                                {
                                                    Editor = new BuildConfigPluginBuildEditor
                                                    {
                                                        Platforms = new[]
                                                        {
                                                            OperatingSystem.IsWindows()
                                                                ? BuildConfigPluginBuildEditorPlatform.Win64
                                                                : BuildConfigPluginBuildEditorPlatform.Mac
                                                        },
                                                    }
                                                },
                                                Tests =
                                                [
                                                    test
                                                ]
                                            }
                                        }
                                    };
                                    buildSpec = await _buildSpecificationGenerator.BuildConfigPluginToBuildSpecAsync(
                                        engineSpec,
                                        buildGraphEnvironment,
                                        syntheticBuildConfigPlugin,
                                        syntheticBuildConfigPlugin.Distributions[0],
                                        repositoryRoot: path.DirectoryPath,
                                        executeBuild: true,
                                        executeTests: true,
                                        executeDeployment: false,
                                        strictIncludes: false,
                                        localExecutor: true,
                                        isPluginRooted: false,
                                        commandlinePluginVersionName: null,
                                        commandlinePluginVersionNumber: null,
                                        skipPackaging: true).ConfigureAwait(false);
                                }
                                else
                                {
                                    _logger.LogError("This type of BuildConfig.json file is not supported.");
                                    return 1;
                                }
                                break;
                            }
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
                        null,
                        null,
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
                        foreach (var kv in executionEvents.GetResults())
                        {
                            switch (kv.resultStatus)
                            {
                                case BuildResultStatus.Success:
                                    _logger.LogInformation($"{kv.nodeName} = {Bright.Green("Passed")}");
                                    break;
                                case BuildResultStatus.Failed:
                                    _logger.LogInformation($"{kv.nodeName} = {Bright.Red("Failed")}");
                                    break;
                                case BuildResultStatus.Cancelled:
                                    _logger.LogInformation($"{kv.nodeName} = {Bright.Yellow("Cancelled")}");
                                    break;
                                case BuildResultStatus.NotRun:
                                    _logger.LogInformation($"{kv.nodeName} = {Bright.Cyan("Not Run")}");
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
