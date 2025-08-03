using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Redpoint.KubernetesManager;
using Redpoint.KubernetesManager.Services;
using Redpoint.ProgressMonitor;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using UET.Commands.Cluster;
using UET.Commands.Internal.RkmService;
using UET.Commands.Upgrade;

namespace UET.Commands.Internal.Rkm
{
    internal sealed class RkmServiceCommand
    {
        internal sealed class Options
        {
        }

        public static Command CreateRkmServiceCommand()
        {
            var options = new Options();
            var command = new Command("rkm-service");
            command.AddAllOptions(options);
            command.AddCommonHandler<RunRkmServiceCommandInstance>(
                options,
                services =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        services.AddWindowsService(options =>
                        {
                            options.ServiceName = "rkm";
                        });
                    }
                    services.AddRkmServiceHelpers(true, "rkm");
                    services.AddHostedService<RKMWorker>();
                });
            return command;
        }

        private sealed class RunRkmServiceCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunRkmServiceCommandInstance> _logger;
            private readonly IProgressFactory _progressFactory;
            private readonly IMonitorFactory _monitorFactory;
            private readonly IRkmGlobalRootProvider _rkmGlobalRootProvider;
            private readonly IHostedServiceFromExecutable _hostedServiceFromExecutable;

            public RunRkmServiceCommandInstance(
                ILogger<RunRkmServiceCommandInstance> logger,
                IProgressFactory progressFactory,
                IMonitorFactory monitorFactory,
                IRkmGlobalRootProvider rkmGlobalRootProvider,
                IHostedServiceFromExecutable hostedServiceFromExecutable)
            {
                _logger = logger;
                _progressFactory = progressFactory;
                _monitorFactory = monitorFactory;
                _rkmGlobalRootProvider = rkmGlobalRootProvider;
                _hostedServiceFromExecutable = hostedServiceFromExecutable;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
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
                        if (DateTimeOffset.UtcNow > lastCheck.AddMinutes(10))
                        {
                            _logger.LogInformation("RKM is checking for UET updates, and upgrading UET if necessary...");
                            var upgradeResult = await UpgradeCommandImplementation.PerformUpgradeAsync(
                                _progressFactory,
                                _monitorFactory,
                                _logger,
                                string.Empty,
                                false,
                                context.GetCancellationToken()).ConfigureAwait(false);
                            if (upgradeResult.CurrentVersionWasChanged)
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
}
