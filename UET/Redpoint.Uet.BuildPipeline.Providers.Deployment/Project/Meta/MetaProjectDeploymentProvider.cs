namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Meta
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Package;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class MetaProjectDeploymentProvider : IProjectDeploymentProvider, IDynamicReentrantExecutor<BuildConfigProjectDistribution, BuildConfigProjectDeploymentMeta>
    {
        private readonly ILogger<MetaProjectDeploymentProvider> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly ISimpleDownloadProgress _simpleDownloadProgress;
        private readonly IGlobalArgsProvider? _globalArgsProvider;

        public MetaProjectDeploymentProvider(
            ILogger<MetaProjectDeploymentProvider> logger,
            IProcessExecutor processExecutor,
            ISimpleDownloadProgress simpleDownloadProgress,
            IGlobalArgsProvider? globalArgsProvider = null)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _simpleDownloadProgress = simpleDownloadProgress;
            _globalArgsProvider = globalArgsProvider;
        }

        public string Type => "Meta";

        public IRuntimeJson DynamicSettings { get; } = new DeploymentProviderRuntimeJson(DeploymentProviderSourceGenerationContext.WithStringEnum).BuildConfigProjectDeploymentMeta;

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IDeploymentProvider>> dynamicSettings)
        {
            var castedSettings = dynamicSettings
                .Select(x => (name: x.Name, manual: x.Manual ?? false, settings: (BuildConfigProjectDeploymentMeta)x.DynamicSettings))
                .ToList();

            // Emit the nodes to run each deployment.
            foreach (var (name, manual, settings) in castedSettings)
            {
                var stageName = $"{settings.Package.Type}Staged_{settings.Package.Target}_{settings.Package.Platform}_{settings.Package.Configuration}";
                if (!string.IsNullOrWhiteSpace(settings.Package.CookFlavor))
                {
                    stageName = $"{stageName}_{settings.Package.CookFlavor}";
                }

                await writer.WriteAgentNodeAsync(
                    new AgentNodeElementProperties
                    {
                        AgentStage = $"Deployment {name}",
                        AgentType = manual ? "Win64_Manual" : "Win64",
                        NodeName = $"Deployment {name}",
                        Requires = $"#{stageName};$(DynamicPreDeploymentNodes)",
                    },
                    async writer =>
                    {
                        await writer.WriteDynamicReentrantSpawnAsync<MetaProjectDeploymentProvider, BuildConfigProjectDistribution, BuildConfigProjectDeploymentMeta>(
                            this,
                            context,
                            $"{settings.Package.Platform}.{name}".Replace(" ", ".", StringComparison.Ordinal),
                            settings,
                            new Dictionary<string, string>
                            {
                                { "ProjectRoot", "$(ProjectRoot)" },
                                { "StageDirectory", "$(StageDirectory)" },
                                { "TempPath", "$(TempPath)" },
                                { "Timestamp", "$(Timestamp)" },
                            }).ConfigureAwait(false);
                        await writer.WriteDynamicNodeAppendAsync(
                            new DynamicNodeAppendElementProperties
                            {
                                NodeName = $"Deployment {name}",
                            }).ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }
        }

        public async Task<int> ExecuteBuildGraphNodeAsync(
            object configUnknown,
            Dictionary<string, string> runtimeSettings,
            CancellationToken cancellationToken)
        {
            var config = (BuildConfigProjectDeploymentMeta)configUnknown;

            var stagePlatform = config.Package.Platform;
            if (stagePlatform is not "Android" and not "MetaQuest")
            {
                _logger.LogError("Only Android/MetaQuest builds can be published to Meta Quest.");
                return 1;
            }

            var appSecret = Environment.GetEnvironmentVariable(config.AppSecretEnvVar);
            if (string.IsNullOrWhiteSpace(appSecret))
            {
                _logger.LogError($"Expected the environment variable '{config.AppSecretEnvVar}' to be set and contain the Meta Quest app secret, but it is not set or is an empty string.");
                return 1;
            }

            var ovrPlatformUtil = Path.Combine(Path.GetTempPath(), "ovr-platform-util.exe");
            if (!File.Exists(ovrPlatformUtil))
            {
                var ovrPlatformUtilTemp = Path.Combine(Path.GetTempPath(), $"ovr-platform-util.{Environment.ProcessId}.exe");
                using (var fileWriter = new FileStream(ovrPlatformUtilTemp, FileMode.Create, FileAccess.ReadWrite))
                {
                    _logger.LogInformation("Downloading ovr-platform-util.exe so we can submit builds to Meta...");
                    using var httpClient = new HttpClient();
                    await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                        httpClient,
                        new Uri("https://www.oculus.com/download_app/?id=1076686279105243"),
                        stream => stream.CopyToAsync(fileWriter),
                        cancellationToken).ConfigureAwait(false);
                }
                File.Move(
                    ovrPlatformUtilTemp,
                    ovrPlatformUtil);
            }

            var binariesFolder = Path.Combine(runtimeSettings["ProjectRoot"], "Binaries", stagePlatform);
            if (!string.IsNullOrWhiteSpace(config.Package.CookFlavor))
            {
                binariesFolder = Path.Combine(runtimeSettings["ProjectRoot"], "Binaries", $"{stagePlatform}_{config.Package.CookFlavor}");
            }

            // @note: Is this filename format reliable?
            var apkPath = Path.Combine(binariesFolder, $"{config.Package.Target}-Android-{config.Package.Configuration}-arm64.apk");
            var symbolsPath = Path.Combine(binariesFolder, $"{config.Package.Target}_Symbols_v{runtimeSettings["Timestamp"]}");
            var obbPath = Directory.GetFiles(binariesFolder, $"main.{runtimeSettings["Timestamp"]}.*.obb").FirstOrDefault();

            _logger.LogInformation($"APK located at: {apkPath}");
            if (Directory.Exists(symbolsPath))
            {
                _logger.LogInformation($"Symbols folder located at: {symbolsPath}");
            }
            else
            {
                _logger.LogWarning("No symbols folder found; debug symbols will not be uploaded!");
            }
            if (obbPath != null)
            {
                _logger.LogInformation($"OBB file located at: {apkPath}");
            }
            else
            {
                _logger.LogWarning("No OBB file found; the game is unlikely to start properly unless all required data is packaged into the APK.");
            }

            var arguments = new List<LogicalProcessArgument>
            {
                "upload-quest-build",
                "--app-id",
                config.AppID,
                "--app-secret",
                appSecret,
                "--channel",
                config.Channel,
                "--apk",
                apkPath,
            };
            if (Directory.Exists(symbolsPath))
            {
                arguments.Add("--debug-symbols-dir");
                arguments.Add(symbolsPath);
            }
            if (obbPath != null)
            {
                arguments.Add("--obb");
                arguments.Add(obbPath);
            }

            _logger.LogInformation($"Deploying to Meta Quest...");
            return await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = ovrPlatformUtil,
                    Arguments = arguments
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
