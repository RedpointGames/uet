using Microsoft.Extensions.Logging;
using Redpoint.CommandLine;
using Redpoint.KubernetesManager.Abstractions;
using Redpoint.KubernetesManager.Services;
using Redpoint.ProgressMonitor;
using System.Globalization;
using UET.Commands.Cluster;

namespace UET.Commands.Internal.Rkm
{
    internal sealed class RunRkmServiceCommandInstance : ICommandInstance
    {
        private readonly ILogger<RunRkmServiceCommandInstance> _logger;
        private readonly IProgressFactory _progressFactory;
        private readonly IMonitorFactory _monitorFactory;
        private readonly IRkmGlobalRootProvider _rkmGlobalRootProvider;
        private readonly IHostedServiceFromExecutable _hostedServiceFromExecutable;
        private readonly IRkmSelfUpgradeService? _rkmSelfUpgradeService;

        public RunRkmServiceCommandInstance(
            ILogger<RunRkmServiceCommandInstance> logger,
            IProgressFactory progressFactory,
            IMonitorFactory monitorFactory,
            IRkmGlobalRootProvider rkmGlobalRootProvider,
            IHostedServiceFromExecutable hostedServiceFromExecutable,
            IRkmSelfUpgradeService? rkmSelfUpgradeService = null)
        {
            _logger = logger;
            _progressFactory = progressFactory;
            _monitorFactory = monitorFactory;
            _rkmGlobalRootProvider = rkmGlobalRootProvider;
            _hostedServiceFromExecutable = hostedServiceFromExecutable;
            _rkmSelfUpgradeService = rkmSelfUpgradeService;
        }

        public async Task<int> ExecuteAsync(ICommandInvocationContext context)
        {
            if (File.Exists(Path.Combine(_rkmGlobalRootProvider.RkmGlobalRoot, "service-auto-upgrade")))
            {
                try
                {
                    var lastCheck = DateTimeOffset.MinValue;
                    var lastCheckFile = Path.Combine(_rkmGlobalRootProvider.RkmGlobalRoot, "service-auto-upgrade-last-check");
                    try
                    {
                        lastCheck = DateTimeOffset.FromUnixTimeSeconds(long.Parse(File.ReadAllText(lastCheckFile).Trim(), CultureInfo.InvariantCulture));
                    }
                    catch
                    {
                    }

                    // Prevent us from running checks against GitHub too rapidly.
                    if (_rkmSelfUpgradeService != null && DateTimeOffset.UtcNow > lastCheck.AddMinutes(10))
                    {
                        _logger.LogInformation("RKM is checking for UET updates, and upgrading UET if necessary...");
                        var currentVersionWasChanged = await _rkmSelfUpgradeService.UpgradeIfNeededAsync(context.GetCancellationToken()).ConfigureAwait(false);
                        if (currentVersionWasChanged)
                        {
                            _logger.LogInformation("UET has been upgraded and the version currently executing is no longer the latest version. RKM will now exit and expects the service manager (such as systemd) to automatically start it RKM as the new version.");
                            return 0;
                        }

                        // Only update the last check file if we didn't upgrade; this helps us get to the latest version faster if multiple upgrades are required.
                        File.WriteAllText(lastCheckFile, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        _logger.LogInformation("RKM already checked for UET upgrades in the last 10 minutes. Skipping automatic upgrade check.");
                    }
                }
                catch
                {
                }
            }
            else
            {
                _logger.LogInformation("RKM is not automatically checking for updates. Run 'uet cluster start --auto-upgrade' to enable automatic updates.");
            }

            _logger.LogInformation("RKM is starting...");

            await _hostedServiceFromExecutable.RunHostedServicesAsync(context.GetCancellationToken());

            return 0;
        }
    }
}
