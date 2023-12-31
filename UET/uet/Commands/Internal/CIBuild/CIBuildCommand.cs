﻿namespace UET.Commands.Internal.CIBuild
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.BuildPipeline.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Environment;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.BuildServer;
    using Redpoint.Uet.BuildPipeline.Executors.GitLab;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Diagnostics;
    using System.Text.Json;
    using System.Threading.Tasks;
    using UET.Commands.EngineSpec;

    internal class CIBuildCommand
    {
        internal class Options
        {
            public Option<string> Executor;

            public Options()
            {
                Executor = new Option<string>(
                    "--executor",
                    description: "The executor to use.",
                    getDefaultValue: () => "gitlab");
                Executor.AddAlias("-x");
                Executor.FromAmong("gitlab");
            }
        }

        public static Command CreateCIBuildCommand()
        {
            var options = new Options();
            var command = new Command("ci-build", "Build a single node of a BuildGraph job from a build server.");
            command.AddAllOptions(options);
            command.AddCommonHandler<CIBuildCommandInstance>(options);
            return command;
        }

        private class CIBuildCommandInstance : ICommandInstance
        {
            private readonly ILogger<CIBuildCommandInstance> _logger;
            private readonly Options _options;
            private readonly GitLabBuildExecutorFactory _gitLabBuildExecutorFactory;

            public CIBuildCommandInstance(
                ILogger<CIBuildCommandInstance> logger,
                Options options,
                GitLabBuildExecutorFactory gitLabBuildExecutorFactory)
            {
                _logger = logger;
                _options = options;
                _gitLabBuildExecutorFactory = gitLabBuildExecutorFactory;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var executorName = context.ParseResult.GetValueForOption(_options.Executor);

                var buildJsonRaw = Environment.GetEnvironmentVariable("UET_BUILD_JSON");
                if (string.IsNullOrWhiteSpace(buildJsonRaw))
                {
                    _logger.LogError("The UET_BUILD_JSON environment variable is not set or is empty.");
                    return 1;
                }

                var buildJson = JsonSerializer.Deserialize(buildJsonRaw, BuildJobJsonSourceGenerationContext.Default.BuildJobJson);
                if (buildJson == null)
                {
                    _logger.LogError("The UET_BUILD_JSON environment variable does not contain a valid build job description.");
                    return 1;
                }

                var engine = EngineSpec.TryParseEngineSpecExact(buildJson.Engine);
                if (engine == null)
                {
                    _logger.LogError($"The engine '{buildJson.Engine}' could not be located on this machine.");
                    return 1;
                }

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

                IBuildNodeExecutor executor;
                switch (executorName)
                {
                    case "gitlab":
                        executor = _gitLabBuildExecutorFactory.CreateNodeExecutor();
                        break;
                    default:
                        throw new NotSupportedException();
                }

                BuildGraphEnvironment environment;
                if (OperatingSystem.IsWindows())
                {
                    environment = new BuildGraphEnvironment
                    {
                        PipelineId = executor.DiscoverPipelineId(),
                        Windows = new BuildGraphWindowsEnvironment
                        {
                            SharedStorageAbsolutePath = buildJson.SharedStoragePath,
                            SdksPath = buildJson.SdksPath,
                        },
                        Mac = null!,
                        UseStorageVirtualisation = Environment.GetEnvironmentVariable("UET_USE_STORAGE_VIRTUALIZATION") != "false",
                    };
                }
                else if (OperatingSystem.IsMacOS())
                {
                    environment = new BuildGraphEnvironment
                    {
                        PipelineId = executor.DiscoverPipelineId(),
                        Windows = null!,
                        Mac = new BuildGraphMacEnvironment
                        {
                            SharedStorageAbsolutePath = buildJson.SharedStoragePath,
                            SdksPath = buildJson.SdksPath,
                        },
                        UseStorageVirtualisation = Environment.GetEnvironmentVariable("UET_USE_STORAGE_VIRTUALIZATION") != "false",
                    };
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                var buildSpecification = new BuildSpecification
                {
                    Engine = engineSpec,
                    BuildGraphScript = BuildGraphScriptSpecification.FromReparsableString(buildJson.BuildGraphScriptName),
                    BuildGraphTarget = buildJson.BuildGraphTarget,
                    BuildGraphSettings = buildJson.Settings,
                    BuildGraphEnvironment = environment,
                    BuildGraphPreparationScripts = buildJson.PreparationScripts.ToList(),
                    // @note: CI executors ignore this field and look at environment variables to figure out
                    // what commit they need to checkout via the workspace APIs.
                    BuildGraphRepositoryRoot = string.Empty,
                    BuildGraphSettingReplacements = new Dictionary<string, string>(),
                    DistributionName = buildJson.DistributionName,
                    UETPath = Process.GetCurrentProcess().MainModule!.FileName,
                    ProjectFolderName = buildJson.ProjectFolderName,
                    GlobalEnvironmentVariables = buildJson.GlobalEnvironmentVariables,
                    ArtifactExportPath = Environment.CurrentDirectory,
                };

                try
                {
                    var buildResult = await executor.ExecuteBuildNodeAsync(
                        buildSpecification,
                        new LoggerBasedBuildExecutionEvents(_logger),
                        buildJson.NodeName,
                        context.GetCancellationToken());
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
