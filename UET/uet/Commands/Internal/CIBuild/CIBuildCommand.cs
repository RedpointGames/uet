namespace UET.Commands.Internal.CIBuild
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.BuildPipeline.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Environment;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.BuildServer;
    using Redpoint.Uet.BuildPipeline.Executors.GitLab;
    using Redpoint.Uet.Core.Permissions;
    using Redpoint.Uet.Workspace;
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

        private sealed class CIBuildCommandInstance : ICommandInstance
        {
            private readonly ILogger<CIBuildCommandInstance> _logger;
            private readonly Options _options;
            private readonly GitLabBuildExecutorFactory _gitLabBuildExecutorFactory;
            private readonly IWorldPermissionApplier _worldPermissionApplier;
            private readonly IDynamicWorkspaceProvider _dynamicWorkspaceProvider;
            private readonly BuildJobJsonSourceGenerationContext _buildJobJsonSourceGenerationContext;

            public CIBuildCommandInstance(
                ILogger<CIBuildCommandInstance> logger,
                Options options,
                GitLabBuildExecutorFactory gitLabBuildExecutorFactory,
                IWorldPermissionApplier worldPermissionApplier,
                IServiceProvider serviceProvider,
                IDynamicWorkspaceProvider dynamicWorkspaceProvider)
            {
                _logger = logger;
                _options = options;
                _gitLabBuildExecutorFactory = gitLabBuildExecutorFactory;
                _worldPermissionApplier = worldPermissionApplier;
                _dynamicWorkspaceProvider = dynamicWorkspaceProvider;
                _buildJobJsonSourceGenerationContext = BuildJobJsonSourceGenerationContext.Create(serviceProvider);
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

                var buildJson = JsonSerializer.Deserialize(buildJsonRaw, _buildJobJsonSourceGenerationContext.BuildJobJson);
                if (buildJson == null)
                {
                    _logger.LogError("The UET_BUILD_JSON environment variable does not contain a valid build job description.");
                    return 1;
                }

                // If we have runbacks enabled, serialize our runback information and emit the ID
                // to the console.
                if (Environment.GetEnvironmentVariable("UET_RUNBACKS") == "1")
                {
                    var runbackId = Guid.NewGuid().ToString();
                    var runbackPath = true switch
                    {
                        var v when v == OperatingSystem.IsWindows() => Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                            "UET",
                            "Runbacks",
                            $"{runbackId}.json"),
                        var v when v == OperatingSystem.IsMacOS() => Path.Combine(
                            "/Users/Shared",
                            "UET",
                            "Runbacks",
                            $"{runbackId}.json"),
                        _ => throw new PlatformNotSupportedException("This platform is not supported for runbacks.")
                    };
                    var runbackJson = new RunbackJson
                    {
                        RunbackId = runbackId,
                        BuildJson = buildJson,
                        EnvironmentVariables = Environment.GetEnvironmentVariables().OfType<KeyValuePair<string, string>>().ToDictionary(k => k.Key, v => v.Value),
                        WorkingDirectory = Environment.CurrentDirectory,
                    };
                    Directory.CreateDirectory(Path.GetDirectoryName(runbackPath)!);
                    await File.WriteAllTextAsync(
                        runbackPath,
                        JsonSerializer.Serialize(runbackJson, RunbackJsonSerializerContext.Default.RunbackJson)).ConfigureAwait(false);
                    _logger.LogInformation($"Runback information saved. You can run this job again on this machine outside CI by running: 'uet internal runback {runbackId}'.");
                }

                // Configure the dynamic workspace provider to use workspace virtualisation
                // if appropriate.
                _dynamicWorkspaceProvider.UseWorkspaceVirtualisation = buildJson.UseStorageVirtualisation;

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
                    case EngineSpecType.GitCommit:
                        engineSpec = BuildEngineSpecification.ForGitCommitWithZips(
                            engine.GitUrl!,
                            engine.GitCommit!,
                            engine.ZipLayers,
                            isEngineBuild: buildJson.IsEngineBuild,
                            windowsSharedGitCachePath: engine.WindowsSharedGitCachePath,
                            macSharedGitCachePath: engine.MacSharedGitCachePath);
                        break;
                    case EngineSpecType.SelfEngineByBuildConfig:
                        throw new InvalidOperationException("EngineSpec.TryParseEngineSpecExact should not be able to return EngineSpecType.SelfEngineByBuildConfig");
                    default:
                        throw new NotSupportedException($"The EngineSpecType {engine.Type} is not supported by the 'ci-build' command.");
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
                        UseStorageVirtualisation = buildJson.UseStorageVirtualisation,
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
                        UseStorageVirtualisation = buildJson.UseStorageVirtualisation,
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
                    var buildResult = await executor.ExecuteBuildNodeAsync(
                        buildSpecification,
                        buildJson.PreparePlugin,
                        buildJson.PrepareProject,
                        new LoggerBasedBuildExecutionEvents(_logger),
                        buildJson.NodeName,
                        context.GetCancellationToken()).ConfigureAwait(false);
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
                    await _worldPermissionApplier.GrantEveryonePermissionAsync(Path.Combine(buildJson.SharedStoragePath, buildJson.NodeName), context.GetCancellationToken()).ConfigureAwait(false);
                }
            }
        }
    }
}
