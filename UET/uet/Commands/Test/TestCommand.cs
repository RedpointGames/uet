namespace UET.Commands.Test
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.Local;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Automation;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Automation;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Core;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using UET.Commands.Build;
    using UET.Commands.EngineSpec;
    using static Crayon.Output;

    internal class TestCommand
    {
        internal class Options
        {
            public Option<EngineSpec> Engine;
            public Option<PathSpec> Path;

            public Option<string> Prefix;

            public Options()
            {
                Path = new Option<PathSpec>(
                    "--path",
                    description: "The directory path that contains a .uproject file or a .uplugin file. BuildConfig.json files will be ignored for this command; if you need to run tests for a BuildConfig.json, use the 'build' command instead with the --test option.",
                    parseArgument: result => PathSpec.ParsePathSpec(result, true),
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
            }
        }

        public static Command CreateTestCommand()
        {
            var command = new Command("test", "Run automation tests in the editor for a project or plugin.");
            command.AddServicedOptionsHandler<TestCommandInstance, Options>(
                services =>
                {
                    services.AddSingleton<IBuildSpecificationGenerator, DefaultBuildSpecificationGenerator>();
                });
            return command;
        }

        private class TestCommandInstance : ICommandInstance
        {
            private readonly ILogger<TestCommandInstance> _logger;
            private readonly Options _options;
            private readonly IBuildSpecificationGenerator _buildSpecificationGenerator;
            private readonly LocalBuildExecutorFactory _localBuildExecutorFactory;
            private readonly IStringUtilities _stringUtilities;

            public TestCommandInstance(
                ILogger<TestCommandInstance> logger,
                Options options,
                IBuildSpecificationGenerator buildSpecificationGenerator,
                LocalBuildExecutorFactory localBuildExecutorFactory,
                IStringUtilities stringUtilities)
            {
                _logger = logger;
                _options = options;
                _buildSpecificationGenerator = buildSpecificationGenerator;
                _localBuildExecutorFactory = localBuildExecutorFactory;
                _stringUtilities = stringUtilities;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var engine = context.ParseResult.GetValueForOption(_options.Engine)!;
                var path = context.ParseResult.GetValueForOption(_options.Path)!;
                var prefix = context.ParseResult.GetValueForOption(_options.Prefix)!;

                // @todo: Should we surface these options?
                var windowsSharedStoragePath = Path.Combine(path.DirectoryPath, ".uet", "shared-storage");
                var macSharedStoragePath = Path.Combine(path.DirectoryPath, ".uet", "shared-storage");
                var windowsSdksPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "UET",
                    "SDKs");
                var macSdksPath = "/Users/Shared/UET/SDKs";

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
                            // Use heuristics to guess the targets for this build.
                            string editorTarget;
                            string gameTarget;
                            if (Directory.Exists(Path.Combine(path.DirectoryPath, "Source")))
                            {
                                var files = Directory.GetFiles(Path.Combine(path.DirectoryPath, "Source"), "*.Target.cs");
                                editorTarget = files.Where(x => x.EndsWith("Editor.Target.cs")).Select(x => Path.GetFileName(x)).First();
                                editorTarget = editorTarget.Substring(0, editorTarget.LastIndexOf(".Target.cs"));
                                gameTarget = editorTarget.Substring(0, editorTarget.LastIndexOf("Editor"));
                            }
                            else
                            {
                                editorTarget = "UnrealEditor";
                                gameTarget = "UnrealGame";
                            }

                            var buildConfigProject = new BuildConfigProject
                            {
                                Type = Redpoint.Uet.Configuration.BuildConfigType.Plugin,
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
                                                    TestPrefix = prefix,
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
                                buildConfigProject.Distributions[0],
                                repositoryRoot: path.DirectoryPath,
                                executeBuild: true,
                                executeTests: true,
                                executeDeployment: false,
                                strictIncludes: false,
                                localExecutor: true);
                            break;
                        case PathSpecType.UPlugin:
                            var buildConfigPlugin = new BuildConfigPlugin
                            {
                                PluginName = Path.GetFileNameWithoutExtension(path.UPluginPath!),
                                Type = Redpoint.Uet.Configuration.BuildConfigType.Plugin,
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
                                                    TestPrefix = prefix,
                                                }
                                            }
                                        }
                                    }
                                }
                            };
                            buildSpec = await _buildSpecificationGenerator.BuildConfigPluginToBuildSpecAsync(
                                engineSpec,
                                buildGraphEnvironment,
                                buildConfigPlugin.Distributions[0],
                                buildConfigPlugin,
                                repositoryRoot: path.DirectoryPath,
                                executeBuild: true,
                                executePackage: true,
                                executeTests: true,
                                executeDeployment: false,
                                strictIncludes: false,
                                localExecutor: true,
                                isPluginRooted: true,
                                commandlinePluginVersionName: null,
                                commandlinePluginVersionNumber: null);
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
                        null,
                        null,
                        executionEvents,
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
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
                catch (BuildPipelineExecutionFailure ex)
                {
                    _logger.LogError(ex.Message);
                    return 1;
                }
            }
        }
    }
}
