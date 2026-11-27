namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.PartnerCenter
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.PackageManagement;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.Reservation;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.GooglePlay;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Text.Json;
    using System.Xml;

    internal class PartnerCenterProjectDeploymentProvider : IProjectDeploymentProvider, IDynamicReentrantExecutor<BuildConfigProjectDistribution, BuildConfigProjectDeploymentPartnerCenter>
    {
        private readonly ILogger<PartnerCenterProjectDeploymentProvider> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly ISimpleDownloadProgress _simpleDownloadProgress;
        private readonly IPackageManager _packageManager;
        private readonly IPathResolver _pathResolver;
        private readonly IGlobalMutexReservationManager _globalMutexReservationManager;
        private readonly IGlobalArgsProvider? _globalArgsProvider;

        public PartnerCenterProjectDeploymentProvider(
            ILogger<PartnerCenterProjectDeploymentProvider> logger,
            IProcessExecutor processExecutor,
            ISimpleDownloadProgress simpleDownloadProgress,
            IPackageManager packageManager,
            IReservationManagerFactory reservationManagerFactory,
            IPathResolver pathResolver,
            IGlobalArgsProvider? globalArgsProvider = null)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _simpleDownloadProgress = simpleDownloadProgress;
            _packageManager = packageManager;
            _pathResolver = pathResolver;
            _globalMutexReservationManager = reservationManagerFactory.CreateGlobalMutexReservationManager();
            _globalArgsProvider = globalArgsProvider;
        }

        public string Type => "PartnerCenter";

        public IRuntimeJson DynamicSettings { get; } = new DeploymentProviderRuntimeJson(DeploymentProviderSourceGenerationContext.WithStringEnum).BuildConfigProjectDeploymentPartnerCenter;


        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IDeploymentProvider>> dynamicSettings)
        {
            var castedSettings = dynamicSettings
                .Select(x => (name: x.Name, manual: x.Manual ?? false, settings: (BuildConfigProjectDeploymentPartnerCenter)x.DynamicSettings))
                .ToList();

            // Emit the nodes to run each deployment.
            foreach (var (name, manual, settings) in castedSettings)
            {
                var stageNames = new List<string>();
                foreach (var package in settings.Packages)
                {
                    var stageName = $"{package.Type}Staged_{package.Target}_{package.Platform}_{package.Configuration}";
                    if (!string.IsNullOrWhiteSpace(package.CookFlavor))
                    {
                        stageName = $"{stageName}_{package.CookFlavor}";
                    }
                    stageNames.Add($"#{stageName}");
                }

                await writer.WriteAgentNodeAsync(
                    new AgentNodeElementProperties
                    {
                        AgentStage = $"Deployment {name}",
                        AgentType = manual ? "Win64_Manual" : "Win64",
                        NodeName = $"Deployment {name}",
                        Requires = $"{string.Join(';', stageNames)};$(DynamicPreDeploymentNodes)",
                    },
                    async writer =>
                    {
                        await writer.WriteDynamicReentrantSpawnAsync<PartnerCenterProjectDeploymentProvider, BuildConfigProjectDistribution, BuildConfigProjectDeploymentPartnerCenter>(
                            this,
                            context,
                            $"{string.Join('.', settings.Packages.Select(x => x.Platform))}.{name}".Replace(" ", ".", StringComparison.Ordinal),
                            settings,
                            new Dictionary<string, string>
                            {
                                { "ProjectRoot", "$(ProjectRoot)" },
                                { "StageDirectory", "$(StageDirectory)" },
                                { "TempPath", "$(TempPath)" },
                                { "Timestamp", "$(Timestamp)" },
                                { "ReleaseVersion", "$(ReleaseVersion)" },
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
            var config = (BuildConfigProjectDeploymentPartnerCenter)configUnknown;

            await using (await _globalMutexReservationManager.ReserveExactAsync("XboxPackageManager", cancellationToken))
            {
                _logger.LogInformation("Installing Xbox Game Package Manager...");
                await _packageManager.InstallOrUpgradePackageToLatestAsync(
                    "Microsoft.Gaming.GDK.XboxGamePackageManager",
                     cancellationToken: cancellationToken);
            }

            var releaseVersion = runtimeSettings["ReleaseVersion"];

            var tenantId = Environment.GetEnvironmentVariable("UET_PARTNER_CENTER_TENANT_ID");
            var clientId = Environment.GetEnvironmentVariable("UET_PARTNER_CENTER_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("UET_PARTNER_CENTER_CLIENT_SECRET");
            if (string.IsNullOrWhiteSpace(tenantId) ||
                string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(clientSecret))
            {
                _logger.LogError("Missing UET_PARTNER_CENTER_TENANT_ID, UET_PARTNER_CENTER_CLIENT_ID or UET_PARTNER_CENTER_CLIENT_SECRET environment variables, which is required for Partner Center deployments. Follow the instructions at https://github.com/microsoft/PackageUploader#service-creation-and-authentication to create these values and then set the environment variables on the command line or in your build server configuration.");
                return 1;
            }

            {
                var operationConfig = new PartnerCenterConfigFileRemovePackages
                {
                    OperationName = "RemovePackages",
                    AadAuthInfo = new PartnerCenterAadAuthInfo
                    {
                        TenantId = tenantId,
                        ClientId = clientId,
                    },
                    BigId = config.ProductId,
                    BranchFriendlyName = config.BranchDisplayName,
                    PackageFileName = "*",
                };
                var operationConfigFilePath = Path.GetTempFileName();
                try
                {
                    await File.WriteAllTextAsync(
                        operationConfigFilePath,
                        JsonSerializer.Serialize(operationConfig, DeploymentProviderSourceGenerationContext.Default.PartnerCenterConfigFileRemovePackages),
                        cancellationToken);

                    _logger.LogInformation($"Removing existing draft packages from '{config.PublishToSandbox}' sandbox...");
                    var exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = await _pathResolver.ResolveBinaryPath("packageuploadercmd"),
                            Arguments =
                            [
                                "RemovePackages",
                                "-a",
                                "AppSecret",
                                "-s",
                                clientSecret,
                                "-c",
                                operationConfigFilePath,
                            ]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                    if (exitCode != 0)
                    {
                        _logger.LogError($"Failed to remove existing draft packages in Partner Center for '{config.PublishToSandbox}' sandbox.");
                        return exitCode;
                    }
                }
                finally
                {
                    File.Delete(operationConfigFilePath);
                }
            }

            foreach (var package in config.Packages)
            {
                var stagedPath =
                    package.Platform == "Win64"
                    ? Path.Combine(runtimeSettings["ProjectRoot"], "Releases", releaseVersion, "Windows", "MSGameStore", package.Configuration)
                    : Path.Combine(runtimeSettings["ProjectRoot"], "Releases", releaseVersion, package.Platform, package.Configuration);
                if (!Directory.Exists(stagedPath))
                {
                    _logger.LogError($"Expected directory '{stagedPath}' to exist after build.");
                    return 1;
                }
                var packageExtension = package.Platform == "Win64" ? "msixvc" : "xvc";
                var packageFilePath = Directory.GetFiles(stagedPath, $"*.{packageExtension}").FirstOrDefault();
                var ekbFilePath = Directory.GetFiles(stagedPath, "*.ekb").FirstOrDefault();
                var subValFilePath = Directory.GetFiles(stagedPath, "Validator_*.xml").FirstOrDefault();
                var symbolsFilePath = Directory.GetFiles(stagedPath, "*.zip").FirstOrDefault();
                var discLayoutFilePath = Directory.GetFiles(stagedPath, "layout_*.xml").FirstOrDefault();
                if (packageFilePath == null)
                {
                    _logger.LogError($"Could not find *.{packageExtension} file for upload within '{stagedPath}'.");
                    return 1;
                }
                if (ekbFilePath == null)
                {
                    _logger.LogError($"Could not find *.ekb file for upload within '{stagedPath}'.");
                    return 1;
                }
                if (subValFilePath == null)
                {
                    _logger.LogError($"Could not find Validator_*.xml file for upload within '{stagedPath}'.");
                    return 1;
                }

                var operationConfig = new PartnerCenterConfigFileUploadXvcPackage
                {
                    OperationName = "UploadXvcPackage",
                    AadAuthInfo = new PartnerCenterAadAuthInfo
                    {
                        TenantId = tenantId,
                        ClientId = clientId,
                    },
                    BigId = config.ProductId,
                    BranchFriendlyName = config.BranchDisplayName,
                    DeltaUpload = true,
                    PackageFilePath = packageFilePath,
                    GameAssets = new PartnerCenterGameAssets
                    {
                        EkbFilePath = ekbFilePath,
                        SubValFilePath = subValFilePath,
                        SymbolsFilePath = symbolsFilePath,
                        DiscLayoutFilePath = discLayoutFilePath,
                        SodbFilePath = null,
                    }
                };
                var operationConfigFilePath = Path.GetTempFileName();
                try
                {
                    await File.WriteAllTextAsync(
                        operationConfigFilePath,
                        JsonSerializer.Serialize(operationConfig, DeploymentProviderSourceGenerationContext.Default.PartnerCenterConfigFileUploadXvcPackage),
                        cancellationToken);

                    _logger.LogInformation($"Uploading '{package.Platform}' package to Partner Center branch '{config.BranchDisplayName}'...");
                    var exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = await _pathResolver.ResolveBinaryPath("packageuploadercmd"),
                            Arguments =
                            [
                                "UploadXvcPackage",
                                "-a",
                                "AppSecret",
                                "-s",
                                clientSecret,
                                "-c",
                                operationConfigFilePath,
                            ]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                    if (exitCode != 0)
                    {
                        _logger.LogError($"Failed to upload '{package.Platform}' package to Partner Center.");
                        return exitCode;
                    }
                }
                finally
                {
                    File.Delete(operationConfigFilePath);
                }
            }

            if (!string.IsNullOrWhiteSpace(config.PublishToSandbox))
            {
                var operationConfig = new PartnerCenterConfigFilePublishPackages
                {
                    OperationName = "PublishPackages",
                    AadAuthInfo = new PartnerCenterAadAuthInfo
                    {
                        TenantId = tenantId,
                        ClientId = clientId,
                    },
                    BigId = config.ProductId,
                    BranchFriendlyName = config.BranchDisplayName,
                    DestinationSandboxName = config.PublishToSandbox
                };
                var operationConfigFilePath = Path.GetTempFileName();
                try
                {
                    await File.WriteAllTextAsync(
                        operationConfigFilePath,
                        JsonSerializer.Serialize(operationConfig, DeploymentProviderSourceGenerationContext.Default.PartnerCenterConfigFilePublishPackages),
                        cancellationToken);

                    _logger.LogInformation($"Publishing packages to '{config.PublishToSandbox}' sandbox...");
                    var exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = await _pathResolver.ResolveBinaryPath("packageuploadercmd"),
                            Arguments =
                            [
                                "PublishPackages",
                                "-a",
                                "AppSecret",
                                "-s",
                                clientSecret,
                                "-c",
                                operationConfigFilePath,
                            ]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                    if (exitCode != 0)
                    {
                        _logger.LogError($"Failed to publish packages in Partner Center to '{config.PublishToSandbox}' sandbox.");
                        return exitCode;
                    }
                }
                finally
                {
                    File.Delete(operationConfigFilePath);
                }
            }

            return 0;
        }
    }
}
