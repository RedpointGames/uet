namespace Redpoint.Uet.SdkManagement.Sdk.Discovery
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.PackageManagement;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.Uet.Core;
    using Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk;
    using Redpoint.Uet.SdkManagement.Sdk.GenericPlatform;
    using Redpoint.Uet.SdkManagement.Sdk.MsiExtract;
    using Redpoint.Uet.SdkManagement.Sdk.VersionNumbers;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class DefaultSdkSetupDiscovery : ISdkSetupDiscovery
    {
        private readonly ILogger<DefaultSdkSetupDiscovery> _logger;
        private readonly IServiceProvider _serviceProvider;

        public DefaultSdkSetupDiscovery(
            ILogger<DefaultSdkSetupDiscovery> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async IAsyncEnumerable<ISdkSetup> DiscoverApplicableSdkSetups(string enginePath)
        {
            var sdks = new List<ISdkSetup>();

            var windowsConfigPath = Path.Combine(
                enginePath,
                "Engine",
                "Config",
                "Windows",
                "Windows_SDK.json");
            var androidConfigPath = Path.Combine(
                enginePath,
                "Engine",
                "Config",
                "Android",
                "Android_SDK.json");
            var linuxConfigPath = Path.Combine(
                enginePath,
                "Engine",
                "Config",
                "Linux",
                "Linux_SDK.json");
            var appleConfigPath = Path.Combine(
                enginePath,
                "Engine",
                "Config",
                "Apple",
                "Apple_SDK.json");

            if (OperatingSystem.IsWindows())
            {
                if (File.Exists(windowsConfigPath))
                {
                    yield return new WindowsSdkSetup(
                        _serviceProvider.GetRequiredService<ILogger<WindowsSdkSetup>>(),
                        _serviceProvider.GetRequiredService<IVersionNumberResolver>(),
                        _serviceProvider.GetRequiredService<WindowsSdkInstaller>());
                }

                if (File.Exists(androidConfigPath))
                {
                    yield return new AndroidSdkSetup(
                        _serviceProvider.GetRequiredService<ILogger<AndroidSdkSetup>>(),
                        _serviceProvider.GetRequiredService<IProcessExecutor>(),
                        _serviceProvider.GetRequiredService<IVersionNumberResolver>(),
                        _serviceProvider.GetRequiredService<IPathResolver>());
                }

                if (File.Exists(linuxConfigPath))
                {
                    yield return new LinuxSdkSetup(
                        _serviceProvider.GetRequiredService<ILogger<LinuxSdkSetup>>(),
                        _serviceProvider.GetRequiredService<IProcessExecutor>(),
                        _serviceProvider.GetRequiredService<ISimpleDownloadProgress>(),
                        _serviceProvider.GetRequiredService<IVersionNumberResolver>(),
                        _serviceProvider.GetRequiredService<IPackageManager>(),
                        _serviceProvider.GetRequiredService<IPathResolver>());
                }

                var confidentialPlatformAutoDiscoveryJsonDefaultPath = Path.Combine(
                    enginePath,
                    "UET.ConsoleSDK.json");
                var confidentialPlatformAutoDiscoveryJson = Environment.GetEnvironmentVariable("UET_PLATFORM_SDK_AUTO_DISCOVERY_CONFIG_PATH");
                if (string.IsNullOrWhiteSpace(confidentialPlatformAutoDiscoveryJson) &&
                    File.Exists(confidentialPlatformAutoDiscoveryJsonDefaultPath))
                {
                    confidentialPlatformAutoDiscoveryJson = confidentialPlatformAutoDiscoveryJsonDefaultPath;
                }
                if (!string.IsNullOrWhiteSpace(confidentialPlatformAutoDiscoveryJson))
                {
                    var autoDiscoveryConfig = JsonSerializer.Deserialize(
                        File.ReadAllText(confidentialPlatformAutoDiscoveryJson),
                        new ConfidentialPlatformJsonSerializerContext(new JsonSerializerOptions
                        {
                            Converters =
                            {
                                new JsonStringEnumConverter(),
                            }
                        }).ConfidentialPlatformAutoDiscovery)!;

                    var fileStorageDirectory = new DirectoryInfo(autoDiscoveryConfig.FileStoragePath);
                    var fileStorageDirectories = fileStorageDirectory.GetDirectories();

                    foreach (var discoverer in autoDiscoveryConfig.Discoverers)
                    {
                        var platformConfigPath = Path.Combine(enginePath, discoverer.EnginePlatformConfigJsonPath);
                        if (File.Exists(platformConfigPath))
                        {
                            var platformConfig = JsonSerializer.Deserialize(
                                File.ReadAllText(platformConfigPath),
                                ConfidentialPlatformJsonSerializerContext.Default.GenericPlatformConfig)!;

                            if (platformConfig.MaxVersion == null ||
                                platformConfig.MinVersion == null ||
                                platformConfig.MainVersion == null)
                            {
                                continue;
                            }

                            var minVersion = GenericPlatformVersion.Parse(platformConfig.MinVersion)!;
                            var maxVersion = GenericPlatformVersion.Parse(platformConfig.MaxVersion)!;
                            var mainVersion = GenericPlatformVersion.Parse(platformConfig.MainVersion)!;

                            _logger.LogInformation($"Attempting to discover SDK for {discoverer.PlatformName} between minimum version {minVersion} and maximum version {maxVersion}, with main version {mainVersion}...");

                            var currentProximity = long.MaxValue;
                            string? selectedVersionString = null;
                            foreach (var directory in fileStorageDirectories)
                            {
                                if (directory.Name.StartsWith($"{discoverer.PlatformName}_", StringComparison.OrdinalIgnoreCase))
                                {
                                    var versionString = directory.Name.Substring($"{discoverer.PlatformName}_".Length);
                                    var candidateVersion = GenericPlatformVersion.Parse(versionString);
                                    if (candidateVersion == null)
                                    {
                                        continue;
                                    }

                                    if (GenericPlatformVersion.IsCandidateWithinBounds(candidateVersion, minVersion, maxVersion))
                                    {
                                        var candidateProximity = Math.Abs(candidateVersion - mainVersion);

                                        _logger.LogInformation($"  - Considering candidate version {candidateVersion} with proximity {candidateProximity}...");

                                        if (candidateProximity < currentProximity)
                                        {
                                            currentProximity = candidateProximity;
                                            selectedVersionString = versionString;
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogInformation($"  - Candidate version {candidateVersion} is outside allowed range.");
                                    }
                                }
                            }

                            if (selectedVersionString != null)
                            {
                                _logger.LogInformation($"  - Best candidate version {discoverer.PlatformName} {selectedVersionString} is selected.");

                                yield return new ConfidentialSdkSetup(
                                    discoverer.RecognisedPlatformNamesForInstall,
                                    discoverer.Config,
                                    selectedVersionString,
                                    _serviceProvider.GetRequiredService<IProcessExecutor>(),
                                    _serviceProvider.GetRequiredService<IStringUtilities>(),
                                    _serviceProvider.GetRequiredService<WindowsSdkInstaller>(),
                                    _serviceProvider.GetRequiredService<IVersionNumberResolver>(),
                                    _serviceProvider.GetRequiredService<ILogger<ConfidentialSdkSetup>>(),
                                    _serviceProvider.GetRequiredService<IMsiExtraction>());
                            }
                            else
                            {
                                _logger.LogInformation($"  - No viable version for platform {discoverer.PlatformName}; it will not be available for install.");
                            }
                        }
                    }
                }
            }

            if (File.Exists(appleConfigPath))
            {
                yield return new MacSdkSetup(
                    _serviceProvider.GetRequiredService<ILogger<MacSdkSetup>>(),
                    _serviceProvider.GetRequiredService<IProcessExecutor>(),
                    _serviceProvider.GetRequiredService<IProgressFactory>(),
                    _serviceProvider.GetRequiredService<IMonitorFactory>(),
                    _serviceProvider.GetRequiredService<IVersionNumberResolver>(),
                    _serviceProvider.GetRequiredService<IPackageManager>());
            }
        }

        public async Task<Dictionary<string, HashSet<ISdkSetup>>> DiscoverApplicableSdkSetupsByPlatformName(string enginePath)
        {
            var availableSdkSetups = new Dictionary<string, HashSet<ISdkSetup>>();
            await foreach (var availableSdkSetup in DiscoverApplicableSdkSetups(enginePath))
            {
                foreach (var platformName in availableSdkSetup.PlatformNames)
                {
                    if (!availableSdkSetups.TryGetValue(platformName, out var sdkSetupList))
                    {
                        sdkSetupList = new();
                        availableSdkSetups.Add(platformName, sdkSetupList);
                    }
                    sdkSetupList.Add(availableSdkSetup);
                }
            }
            return availableSdkSetups;
        }

        public void ApplySdkSetupsBasedOnPlatformNames(
            HashSet<ISdkSetup> sdkSetups,
            IEnumerable<string> platformNames,
            Dictionary<string, HashSet<ISdkSetup>> availableSdkSetups)
        {
            foreach (var platform in platformNames)
            {
                if (availableSdkSetups.TryGetValue(platform, out var sdkSetupList))
                {
                    foreach (var sdkSetup in sdkSetupList)
                    {
                        sdkSetups.Add(sdkSetup);
                    }
                }
            }
        }
    }
}
