namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.GooglePlay
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.PackageManagement;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.Reservation;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.GooglePlay;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Workspace.Reservation;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;

    internal class GooglePlayProjectDeploymentProvider : IProjectDeploymentProvider, IDynamicReentrantExecutor<BuildConfigProjectDistribution, BuildConfigProjectDeploymentGooglePlay>
    {
        private readonly ILogger<GooglePlayProjectDeploymentProvider> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly ISimpleDownloadProgress _simpleDownloadProgress;
        private readonly IPackageManager _packageManager;
        private readonly IGlobalMutexReservationManager _globalMutexReservationManager;
        private readonly IGlobalArgsProvider? _globalArgsProvider;

        public GooglePlayProjectDeploymentProvider(
            ILogger<GooglePlayProjectDeploymentProvider> logger,
            IProcessExecutor processExecutor,
            ISimpleDownloadProgress simpleDownloadProgress,
            IPackageManager packageManager,
            IReservationManagerFactory reservationManagerFactory,
            IGlobalArgsProvider? globalArgsProvider = null)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _simpleDownloadProgress = simpleDownloadProgress;
            _packageManager = packageManager;
            _globalMutexReservationManager = reservationManagerFactory.CreateGlobalMutexReservationManager();
            _globalArgsProvider = globalArgsProvider;
        }

        public string Type => "GooglePlay";

        public IRuntimeJson DynamicSettings { get; } = new DeploymentProviderRuntimeJson(DeploymentProviderSourceGenerationContext.WithStringEnum).BuildConfigProjectDeploymentGooglePlay;


        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IDeploymentProvider>> dynamicSettings)
        {
            var castedSettings = dynamicSettings
                .Select(x => (name: x.Name, manual: x.Manual ?? false, settings: (BuildConfigProjectDeploymentGooglePlay)x.DynamicSettings))
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
                        await writer.WriteDynamicReentrantSpawnAsync<GooglePlayProjectDeploymentProvider, BuildConfigProjectDistribution, BuildConfigProjectDeploymentGooglePlay>(
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
            var config = (BuildConfigProjectDeploymentGooglePlay)configUnknown;

            var stagePlatform = config.Package.Platform;
            if (stagePlatform is not "Android" and not "GooglePlay")
            {
                _logger.LogError("Only Android/GooglePlay builds can be published to Google Play.");
                return 1;
            }

            await using (await _globalMutexReservationManager.ReserveExactAsync("RubyInstall", cancellationToken))
            {
                _logger.LogInformation("Installing Ruby...");
                await _packageManager.InstallOrUpgradePackageToLatestAsync(
                    "RubyInstallerTeam.RubyWithDevKit.3.4",
                    cancellationToken);

                _logger.LogInformation("Installing Fastlane...");
                var gemPath = @"C:\Ruby34-x64\bin\gem.cmd";
                var installExitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = gemPath,
                        Arguments = ["install", "fastlane"]
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (installExitCode != 0)
                {
                    return installExitCode;
                }
            }

            var jsonKeyPath = Environment.GetEnvironmentVariable("UET_GOOGLE_PLAY_JSON_KEY_PATH");
            if (string.IsNullOrWhiteSpace(jsonKeyPath))
            {
                if (!string.IsNullOrWhiteSpace(config.JsonKeyPath))
                {
                    if (Path.IsPathRooted(config.JsonKeyPath))
                    {
                        jsonKeyPath = config.JsonKeyPath;
                    }
                    else
                    {
                        jsonKeyPath = Path.Combine(runtimeSettings["ProjectRoot"], config.JsonKeyPath);
                    }
                }
                else
                {
                    _logger.LogError("Missing UET_GOOGLE_PLAY_JSON_KEY_PATH environment variable, which is required for Google Play deployments if you don't set the 'JsonKeyPath' property in the BuildConfig.json file. Set this environment variable on the command line or in your build server configuration.");
                    return 1;
                }
            }

            string? aabPath = null;
            string aabAndroidPath = Path.Combine(runtimeSettings["ProjectRoot"], "Binaries", "Android", $"{config.Package.Target}-Android-Shipping.aab");
            string aabPlatformPath = Path.Combine(runtimeSettings["ProjectRoot"], "Binaries", config.Package.Platform, $"{config.Package.Target}-Android-Shipping.aab");
            if (File.Exists(aabPlatformPath))
            {
                aabPath = aabPlatformPath;
            }
            if (File.Exists(aabAndroidPath))
            {
                if (aabPath == null ||
                    new FileInfo(aabPath).LastWriteTimeUtc < new FileInfo(aabAndroidPath).LastWriteTimeUtc)
                {
                    aabPath = aabAndroidPath;
                }
            }
            if (aabPath == null)
            {
                _logger.LogError($"Could not find AAB at '{aabAndroidPath}' or '{aabPlatformPath}'.");
                return 1;
            }
            _logger.LogInformation($"Using AAB file at path: {aabPath}");

            var fastlanePath = @"C:\Ruby34-x64\bin\fastlane.bat";

            var first = true;
            foreach (var track in config.Tracks.OrderBy(x => x switch
            {
                // Always run out tracks internal -> production, regardless of array order.
                "internal" => 0,
                "alpha" => 1,
                "beta" => 2,
                "production" => 3,
                _ => 4
            }))
            {
                _logger.LogInformation($"Deploying to track '{track}'...");
                var holder = new RetryWithoutUploadHolder();
            retry:

                // The metadata path must be empty (or in future, contain metadata that should be attached to the release).
                var metadataPath = Path.Combine(runtimeSettings["TempPath"], "FastlaneAndroid");
                if (Directory.Exists(metadataPath))
                {
                    await DirectoryAsync.DeleteAsync(metadataPath, true);
                }
                Directory.CreateDirectory(metadataPath);

                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = fastlanePath,
                        Arguments =
                            first
                                ? ["run", "upload_to_play_store"]
                                : ["run", "upload_to_play_store", $"version_codes_to_retain:{runtimeSettings["Timestamp"]}", "rollout:1"],
                        EnvironmentVariables =
                            first
                            ? new Dictionary<string, string>
                            {
                                { "FASTLANE_OPT_OUT_USAGE", "YES" },
                                { "LC_ALL", "en_US.UTF-8" },
                                { "LANG", "en_US.UTF-8" },
                                { "SUPPLY_PACKAGE_NAME", config.PackageName },
                                { "SUPPLY_TRACK", track },
                                { "SUPPLY_JSON_KEY", jsonKeyPath },
                                { "SUPPLY_AAB", aabPath },
                                { "SUPPLY_VERSION_NAME", DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                                { "SUPPLY_METADATA_PATH", metadataPath },
                            }
                            : new Dictionary<string, string>
                            {
                                { "FASTLANE_OPT_OUT_USAGE", "YES" },
                                { "LC_ALL", "en_US.UTF-8" },
                                { "LANG", "en_US.UTF-8" },
                                { "SUPPLY_JSON_KEY", jsonKeyPath },
                                { "SUPPLY_PACKAGE_NAME", config.PackageName },
                                { "SUPPLY_TRACK", track },
                                { "SUPPLY_VERSION_NAME", DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                                { "SUPPLY_VERSION_CODE", runtimeSettings["Timestamp"] },
                            }
                    },
                    first
                        ? CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                        {
                            ReceiveStdout = (line) =>
                            {
                                if (line.Contains("Version code", StringComparison.Ordinal) && line.Contains("has already been used", StringComparison.Ordinal))
                                {
                                    holder.RetryWithoutUpload = true;
                                }
                                return true;
                            },
                            ReceiveStderr = (line) =>
                            {
                                if (line.Contains("Version code", StringComparison.Ordinal) && line.Contains("has already been used", StringComparison.Ordinal))
                                {
                                    holder.RetryWithoutUpload = true;
                                }
                                return true;
                            },
                        })
                        : CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0 && holder.RetryWithoutUpload && first)
                {
                    // We retry assuming that the version was already uploaded. This allows people to retry build jobs without errors.
                    _logger.LogInformation("Detected that this version may already have been uploaded by a previous run. Attempting to run Fastlane again with upload skip...");
                    first = false;
                    goto retry;
                }
                if (exitCode != 0)
                {
                    return exitCode;
                }

                // We don't need to upload on subsequent tracks.
                first = false;
            }

            return 0;
        }

        private class RetryWithoutUploadHolder
        {
            public bool RetryWithoutUpload;
        }
    }
}
