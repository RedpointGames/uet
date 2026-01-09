namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.HostedService;
    using Redpoint.PackageManagement;
    using Redpoint.ServiceControl;
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using System.Threading.Tasks;

    internal class PxeBootMonitorClientCommandInstance : ICommandInstance
    {
        private readonly PxeBootMonitorClientOptions _options;
        private readonly IHostedServiceFromExecutable _hostedServiceFromExecutable;
        private readonly IServiceControl _serviceControl;
        private readonly IPackageManager _packageManager;
        private readonly ILogger<PxeBootMonitorClientCommandInstance> _logger;

        public PxeBootMonitorClientCommandInstance(
            PxeBootMonitorClientOptions options,
            IHostedServiceFromExecutable hostedServiceFromExecutable,
            IServiceControl serviceControl,
            IPackageManager packageManager,
            ILogger<PxeBootMonitorClientCommandInstance> logger)
        {
            _options = options;
            _hostedServiceFromExecutable = hostedServiceFromExecutable;
            _serviceControl = serviceControl;
            _packageManager = packageManager;
            _logger = logger;
        }

        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Auto-detection")]
        private static string GetSelfLocation()
        {
            var assembly = Assembly.GetEntryAssembly();
            if (string.IsNullOrWhiteSpace(assembly?.Location))
            {
#pragma warning disable CA1839 // Use 'Environment.ProcessPath'
                return Process.GetCurrentProcess().MainModule!.FileName;
#pragma warning restore CA1839 // Use 'Environment.ProcessPath'
            }
            else
            {
                var location = assembly.Location;
                if (location.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    // When running via 'dotnet', the .dll file is returned instead of the .exe bootstrapper.
                    // We want to launch via the .exe instead.
                    location = location[..^4] + ".exe";
                }
                return location;
            }
        }

        public async Task<int> ExecuteAsync(ICommandInvocationContext context)
        {
            if (context.ParseResult.GetValueForOption(_options.Install))
            {
                if (await _serviceControl.IsServiceInstalled("rkm-monitor"))
                {
                    if (await _serviceControl.IsServiceRunning("rkm-monitor", context.GetCancellationToken()))
                    {
                        await _serviceControl.StopService(
                            "rkm-monitor",
                            context.GetCancellationToken());
                    }

                    await _serviceControl.UninstallService(
                        "rkm-monitor");
                }

                await _serviceControl.InstallService(
                    "rkm-monitor",
                    "rkm-monitor",
                    $"{GetSelfLocation()} internal pxeboot monitor-client --provisioner-api-address {context.ParseResult.GetValueForOption(_options.ProvisionerApiAddress)}");

                await _serviceControl.StartService(
                    "rkm-monitor",
                    context.GetCancellationToken());
            }
            else
            {
                _logger.LogInformation("Monitoring for reprovision reboot...");
                await _hostedServiceFromExecutable.RunHostedServicesAsync(context.GetCancellationToken());
            }
            return 0;
        }
    }
}
