namespace Redpoint.KubernetesManager.Services.Windows
{
    using Microsoft.Dism;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System.Diagnostics;
    using System.Threading.Tasks;

    internal class WindowsFeatureManager : IWindowsFeatureManager
    {
        private readonly ILogger<WindowsFeatureManager> _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public WindowsFeatureManager(
            ILogger<WindowsFeatureManager> logger,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        public async Task EnsureRequiredFeaturesAreInstalled(bool isController, CancellationToken cancellationToken)
        {
            var didInstall = false;
            var rebootRequired = false;

            DismApi.Initialize(DismLogLevel.LogErrorsWarningsInfo);
            try
            {
                using var session = DismApi.OpenOnlineSessionEx(new DismSessionOptions()
                {
                    ThrowExceptionOnRebootRequired = true,
                });
                var features = DismApi.GetFeatures(session);

                foreach (var feature in features)
                {
                    if (feature.FeatureName == "Containers" &&
                        feature.State != DismPackageFeatureState.Installed)
                    {
                        _logger.LogInformation($"Enabling Windows Containers...");
                        didInstall = true;
                        try
                        {
                            DismApi.EnableFeature(session, feature.FeatureName, false, true, null, progress =>
                            {
                                _logger.LogInformation($"Enabling Windows Containers... {progress.Current / (float)progress.Total * 100:0}% complete");
                            });
                        }
                        catch (DismRebootRequiredException)
                        {
                            rebootRequired = true;
                        }
                    }

                    if (isController)
                    {
                        if (feature.FeatureName == "Microsoft-Windows-Subsystem-Linux" &&
                            feature.State != DismPackageFeatureState.Installed)
                        {
                            _logger.LogInformation($"Enabling Windows Subsystem for Linux...");
                            didInstall = true;
                            try
                            {
                                DismApi.EnableFeature(session, feature.FeatureName, false, true, null, progress =>
                                {
                                    _logger.LogInformation($"Enabling Windows Subsystem for Linux... {progress.Current / (float)progress.Total * 100:0}% complete");
                                });
                            }
                            catch (DismRebootRequiredException)
                            {
                                rebootRequired = true;
                            }
                        }

                        if (feature.FeatureName == "VirtualMachinePlatform" &&
                            feature.State != DismPackageFeatureState.Installed)
                        {
                            _logger.LogInformation($"Enabling Virtual Machine Platform...");
                            didInstall = true;
                            try
                            {
                                DismApi.EnableFeature(session, feature.FeatureName, false, true, null, progress =>
                                {
                                    _logger.LogInformation($"Enabling Virtual Machine Platform... {progress.Current / (float)progress.Total * 100:0}% complete");
                                });
                            }
                            catch (DismRebootRequiredException)
                            {
                                rebootRequired = true;
                            }
                        }
                    }
                }
            }
            finally
            {
                DismApi.Shutdown();
            }

            if (!didInstall)
            {
                _logger.LogInformation($"Windows Containers and WSL features are already enabled.");
            }
            else if (rebootRequired)
            {
                _logger.LogWarning($"Windows Containers/WSL has been enabled, but the computer requires a reboot. Rebooting in 60 seconds...");
                Process.Start("shutdown", "/r /t 60");
                await Task.Delay(120 * 1000, cancellationToken); // Long enough that the shutdown will happen first.
                _hostApplicationLifetime.StopApplication();
            }
        }

    }
}
