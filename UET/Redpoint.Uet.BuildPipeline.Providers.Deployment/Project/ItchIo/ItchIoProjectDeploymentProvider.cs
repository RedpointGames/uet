namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.ItchIo
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Package;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Workspace.Reservation;
    using System;
    using System.Collections.Generic;
    using System.IO.Compression;
    using System.Runtime.InteropServices;
    using System.Xml;

    internal sealed class ItchIoProjectDeploymentProvider : IProjectDeploymentProvider, IDynamicReentrantExecutor<BuildConfigProjectDistribution, BuildConfigProjectDeploymentItchIo>
    {
        private readonly ILogger<ItchIoProjectDeploymentProvider> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly IReservationManagerForUet _reservationManagerForUet;
        private readonly ISimpleDownloadProgress _simpleDownloadProgress;
        private readonly IGlobalArgsProvider? _globalArgsProvider;

        public ItchIoProjectDeploymentProvider(
            ILogger<ItchIoProjectDeploymentProvider> logger,
            IProcessExecutor processExecutor,
            IReservationManagerForUet reservationManagerForUet,
            ISimpleDownloadProgress simpleDownloadProgress,
            IGlobalArgsProvider? globalArgsProvider = null)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _reservationManagerForUet = reservationManagerForUet;
            _simpleDownloadProgress = simpleDownloadProgress;
            _globalArgsProvider = globalArgsProvider;
        }

        public string Type => "ItchIo";

        public IRuntimeJson DynamicSettings { get; } = new DeploymentProviderRuntimeJson(DeploymentProviderSourceGenerationContext.WithStringEnum).BuildConfigProjectDeploymentItchIo;

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IDeploymentProvider>> dynamicSettings)
        {
            var castedSettings = dynamicSettings
                .Select(x => (name: x.Name, manual: x.Manual ?? false, settings: (BuildConfigProjectDeploymentItchIo)x.DynamicSettings))
                .ToList();

            // Emit the nodes to run each deployment.
            foreach (var deployment in castedSettings)
            {
                await writer.WriteAgentNodeAsync(
                    new AgentNodeElementProperties
                    {
                        AgentStage = $"Deployment {deployment.name}",
                        AgentType = deployment.manual ? "Win64_Manual" : "Win64",
                        NodeName = $"Deployment {deployment.name}",
                        Requires = $"#{deployment.settings.Package.Type.ToString()}Staged_{deployment.settings.Package.Target}_{deployment.settings.Package.Platform}_{deployment.settings.Package.Configuration};$(DynamicPreDeploymentNodes)",
                    },
                    async writer =>
                    {
                        await writer.WriteTagAsync(
                            new TagElementProperties
                            {
                                BaseDir = "$(StageDirectory)",
                                Files = "...",
                                Except = $"#{deployment.settings.Package.Type.ToString()}Staged_{deployment.settings.Package.Target}_{deployment.settings.Package.Platform}_{deployment.settings.Package.Configuration}",
                                With = "#FilesToDelete"
                            }).ConfigureAwait(false);
                        await writer.WriteLogAsync(
                            new LogElementProperties
                            {
                                Message = "Extra files detected in staging directory:",
                                Files = "#FilesToDelete"
                            }).ConfigureAwait(false);
                        await writer.WriteDynamicReentrantSpawnAsync<ItchIoProjectDeploymentProvider, BuildConfigProjectDistribution, BuildConfigProjectDeploymentItchIo>(
                            this,
                            context,
                            $"{deployment.settings.Package.Platform}.{deployment.name}".Replace(" ", ".", StringComparison.Ordinal),
                            deployment.settings,
                            new Dictionary<string, string>
                            {
                                { "ProjectRoot", "$(ProjectRoot)" },
                                { "StageDirectory", "$(StageDirectory)" },
                                { "TempPath", "$(TempPath)" },
                            }).ConfigureAwait(false);
                        await writer.WriteDynamicNodeAppendAsync(
                            new DynamicNodeAppendElementProperties
                            {
                                NodeName = $"Deployment {deployment.name}",
                            }).ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }
        }

        public async Task<int> ExecuteBuildGraphNodeAsync(
            object configUnknown,
            Dictionary<string, string> runtimeSettings,
            CancellationToken cancellationToken)
        {
            var config = (BuildConfigProjectDeploymentItchIo)configUnknown;

            var stagePlatform = config.Package.Platform;
            if (stagePlatform == "Win64")
            {
                stagePlatform = "Windows";
            }
            if (config.Package.Type == BuildConfigProjectDeploymentPackageType.Server &&
                (stagePlatform == "Windows" || stagePlatform == "Mac" || stagePlatform == "Linux"))
            {
                stagePlatform = $"{stagePlatform}Server";
            }
            if (config.Package.Type == BuildConfigProjectDeploymentPackageType.Client &&
                (stagePlatform == "Windows" || stagePlatform == "Mac" || stagePlatform == "Linux"))
            {
                stagePlatform = $"{stagePlatform}Client";
            }

            // @note: We don't block when BUTLER_API_KEY isn't set to allow deployments
            // to work with the interactive 'butler login' flow on developer machines.

            string butlerArchitecture;
            if (OperatingSystem.IsWindows())
            {
                butlerArchitecture = "windows-amd64";
            }
            else if (OperatingSystem.IsMacOS())
            {
                if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                {
                    butlerArchitecture = "darwin-arm64";
                }
                else
                {
                    butlerArchitecture = "darwin-amd64";
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                {
                    butlerArchitecture = "linux-arm64";
                }
                else
                {
                    butlerArchitecture = "linux-amd64";
                }
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            var butlerDownloadUrl = $"https://broth.itch.zone/butler/{butlerArchitecture}/LATEST/archive.zip";
            await using ((await _reservationManagerForUet.ReserveExactAsync($"ItchIoButler-{butlerArchitecture}", cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var butlerReservation).ConfigureAwait(false))
            {
                var butlerExecutableName = "butler";
                if (OperatingSystem.IsWindows())
                {
                    butlerExecutableName += ".exe";
                }
                var butlerExecutablePath = Path.Combine(butlerReservation.ReservedPath, butlerExecutableName);

                if (!File.Exists(butlerExecutablePath))
                {
                    using (var client = new HttpClient())
                    {
                        _logger.LogInformation($"Downloading butler: {butlerDownloadUrl}");
                        using (var memoryStream = new MemoryStream())
                        {
                            await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                                client,
                                new Uri(butlerDownloadUrl),
                                stream => stream.CopyToAsync(memoryStream, cancellationToken),
                                cancellationToken).ConfigureAwait(false);
                            memoryStream.Seek(0, SeekOrigin.Begin);

                            var archive = new ZipArchive(memoryStream);
                            archive.ExtractToDirectory(butlerReservation.ReservedPath);
                        }
                    }
                }

                _logger.LogInformation($"Using butler located at: {butlerExecutablePath}");

                var packagePath = Path.Combine(runtimeSettings["StageDirectory"], stagePlatform);
                var projectName = config.Package.Target;

                // @todo: Maybe write a script that can do the EOS / Anti-Cheat prereqs if needed? itch.io doesn't support custom prereqs like Steam.

                File.WriteAllText(
                    Path.Combine(packagePath, ".itch.toml"),
                    $"""
                    [[actions]]
                    name = "play"
                    path = "{projectName}.exe"
                    scope = "profile:me"
                    """);
                try
                {
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = butlerExecutablePath,
                            Arguments = ["push", packagePath, $"{config.Project}:{config.Channel}"]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                }
                finally
                {
                    try
                    {
                        File.Delete(Path.Combine(packagePath, ".itch.toml"));
                    }
                    catch
                    {
                    }
                }
            }

            return 0;
        }
    }
}