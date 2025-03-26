﻿namespace UET.Commands.Internal.CIBuild
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.BuildPipeline.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Environment;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.BuildServer;
    using Redpoint.Uet.BuildPipeline.Executors.GitLab;
    using Redpoint.Uet.BuildPipeline.Executors.Jenkins;
    using Redpoint.Uet.BuildPipeline.Executors.Local;
    using Redpoint.Uet.Core;
    using Redpoint.Uet.Core.Permissions;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Storage;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Diagnostics;
    using System.Text.Json;
    using System.Threading.Tasks;
    using UET.Commands.EngineSpec;
    using UET.Commands.Internal.Runback;

    internal sealed class CIBuildCommand
    {
        internal sealed class Options
        {
            public Option<string> Executor;

            public Options()
            {
                Executor = new Option<string>(
                    "--executor",
                    description: "The executor to use.",
                    getDefaultValue: () => "gitlab");
                Executor.AddAlias("-x");
                Executor.FromAmong("gitlab", "jenkins");
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

        private sealed class CIBuildCommandInstance : ICommandInstance
        {
            private readonly ILogger<CIBuildCommandInstance> _logger;
            private readonly Options _options;
            private readonly GitLabBuildExecutorFactory _gitLabBuildExecutorFactory;
            private readonly JenkinsBuildExecutorFactory _jenkinsBuildExecutorFactory;
            private readonly IWorldPermissionApplier _worldPermissionApplier;
            private readonly IWorkspaceProvider _dynamicWorkspaceProvider;
            private readonly IStorageManagement _storageManagement;
            private readonly BuildJobJsonSourceGenerationContext _buildJobJsonSourceGenerationContext;

            public CIBuildCommandInstance(
                ILogger<CIBuildCommandInstance> logger,
                Options options,
                GitLabBuildExecutorFactory gitLabBuildExecutorFactory,
                JenkinsBuildExecutorFactory jenkinsBuildExecutorFactory,
                IWorldPermissionApplier worldPermissionApplier,
                IServiceProvider serviceProvider,
                IWorkspaceProvider dynamicWorkspaceProvider,
                IStorageManagement storageManagement)
            {
                _logger = logger;
                _options = options;
                _gitLabBuildExecutorFactory = gitLabBuildExecutorFactory;
                _jenkinsBuildExecutorFactory = jenkinsBuildExecutorFactory;
                _worldPermissionApplier = worldPermissionApplier;
                _dynamicWorkspaceProvider = dynamicWorkspaceProvider;
                _storageManagement = storageManagement;
                _buildJobJsonSourceGenerationContext = BuildJobJsonSourceGenerationContext.Create(serviceProvider);
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var executorName = context.ParseResult.GetValueForOption(_options.Executor);

                var buildJsonEnvVar = $"UET_BUILD_JSON";
                var buildJsonRaw = Environment.GetEnvironmentVariable(buildJsonEnvVar);
                if (string.IsNullOrWhiteSpace(buildJsonRaw))
                {
                    _logger.LogError($"The {buildJsonEnvVar} environment variable is not set or is empty.");
                    return 1;
                }

                var buildJson = JsonSerializer.Deserialize(buildJsonRaw, _buildJobJsonSourceGenerationContext.BuildJobJson);
                if (buildJson == null)
                {
                    _logger.LogError($"The {buildJsonEnvVar} environment variable does not contain a valid build job description.");
                    return 1;
                }

                // If we have runbacks enabled, serialize our runback information and emit the ID
                // to the console.
                if (Environment.GetEnvironmentVariable("UET_RUNBACKS") == "1")
                {
                    var runbackJson = new RunbackJson
                    {
                        RunbackId = RunbackGlobalState.RunbackId.ToString(),
                        BuildJson = buildJson,
                        EnvironmentVariables = Environment.GetEnvironmentVariables().OfType<KeyValuePair<string, string>>().ToDictionary(k => k.Key, v => v.Value),
                        WorkingDirectory = Environment.CurrentDirectory,
                    };
                    Directory.CreateDirectory(RunbackGlobalState.RunbackDirectoryPath);
                    await File.WriteAllTextAsync(
                        RunbackGlobalState.RunbackPath,
                        JsonSerializer.Serialize(runbackJson, RunbackJsonSerializerContext.Default.RunbackJson)).ConfigureAwait(false);
                    _logger.LogInformation($"Runback information saved. You can run this job again on this machine outside CI by running: 'uet internal runback {RunbackGlobalState.RunbackId}'.");
                }

                await _storageManagement.AutoPurgeStorageAsync(
                    context.GetCancellationToken()).ConfigureAwait(false);

                var engine = EngineSpec.TryParseEngineSpecExact(buildJson.Engine);
                if (engine == null)
                {
                    _logger.LogError($"The engine '{buildJson.Engine}' could not be located on this machine.");
                    return 1;
                }

                var engineSpec = engine.ToBuildEngineSpecification(
                    "ci-build",
                    null,
                    engine.WindowsSharedGitCachePath,
                    engine.MacSharedGitCachePath);

                var executor = executorName switch
                {
                    "gitlab" => _gitLabBuildExecutorFactory.CreateNodeExecutor(),
                    "jenkins" => _jenkinsBuildExecutorFactory.CreateNodeExecutor(),
                    _ => throw new NotSupportedException(),
                };

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
                            TelemetryPath = buildJson.TelemetryPath,
                        },
                        Mac = null!,
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
                            TelemetryPath = buildJson.TelemetryPath,
                        },
                    };
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

#pragma warning disable CA1839 // Use 'Environment.ProcessPath'
                var buildSpecification = new BuildSpecification
                {
                    Engine = engineSpec,
                    BuildGraphScript = BuildGraphScriptSpecification.FromReparsableString(buildJson.BuildGraphScriptName),
                    BuildGraphTarget = buildJson.BuildGraphTarget,
                    BuildGraphSettings = buildJson.Settings,
                    BuildGraphEnvironment = environment,
                    // @note: CI executors ignore this field and look at environment variables to figure out
                    // what commit they need to checkout via the workspace APIs.
                    BuildGraphRepositoryRoot = string.Empty,
                    BuildGraphSettingReplacements = new Dictionary<string, string>(),
                    DistributionName = buildJson.DistributionName,
                    UETPath = Process.GetCurrentProcess().MainModule!.FileName,
                    ProjectFolderName = buildJson.ProjectFolderName,
                    GlobalEnvironmentVariables = buildJson.GlobalEnvironmentVariables,
                    ArtifactExportPath = Environment.CurrentDirectory,
                    MobileProvisions = buildJson.MobileProvisions,
                };
#pragma warning restore CA1839 // Use 'Environment.ProcessPath'

                try
                {
                    _logger.LogTrace($"{executor.GetType().FullName}.ExecuteBuildNodesAsync starting call.");
                    var buildResult = await executor.ExecuteBuildNodesAsync(
                        buildSpecification,
                        buildJson.PreparePlugin,
                        buildJson.PrepareProject,
                        new LoggerBasedBuildExecutionEvents(_logger),
                        buildJson.NodeNames,
                        context.GetCancellationToken()).ConfigureAwait(false);
                    _logger.LogTrace($"{executor.GetType().FullName}.ExecuteBuildNodesAsync returned build result code '{buildResult}'.");
                    return buildResult;
                }
                catch (BuildPipelineExecutionFailureException ex)
                {
                    _logger.LogError(ex.Message);
                    return 1;
                }
                finally
                {
                    // Update the permissions on our emitted files for this node, so that all machines
                    // have read-write access to the folder. I'm pretty sure BuildGraph already does this
                    // for us, but there are other cases (like when we copy UET to shared storage) that we
                    // need to do permission updates, so let's just do this for consistency.
                    foreach (var nodeName in buildJson.NodeNames)
                    {
                        await _worldPermissionApplier.GrantEveryonePermissionAsync(Path.Combine(buildJson.SharedStoragePath, nodeName), context.GetCancellationToken()).ConfigureAwait(false);
                    }

                    var exitDelaySeconds = Environment.GetEnvironmentVariable("UET_EXIT_DELAY_SECONDS");
                    if (!string.IsNullOrWhiteSpace(exitDelaySeconds) && int.TryParse(exitDelaySeconds, out var delaySeconds))
                    {
                        _logger.LogTrace($"Waiting {delaySeconds} seconds upon exit to allow all logs to be flushed.");
                        await Task.Delay(delaySeconds * 1000).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
