namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.HostedService;
    using Redpoint.KubernetesManager.PxeBoot.SelfLocation;
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
        private readonly ILogger<PxeBootMonitorClientCommandInstance> _logger;

        public PxeBootMonitorClientCommandInstance(
            PxeBootMonitorClientOptions options,
            IHostedServiceFromExecutable hostedServiceFromExecutable,
            IServiceControl serviceControl,
            ILogger<PxeBootMonitorClientCommandInstance> logger)
        {
            _options = options;
            _hostedServiceFromExecutable = hostedServiceFromExecutable;
            _serviceControl = serviceControl;
            _logger = logger;
        }

        private const string _serviceName = "rkm-monitor";

        public async Task<int> ExecuteAsync(ICommandInvocationContext context)
        {
            if (context.ParseResult.GetValueForOption(_options.Install))
            {
                if (await _serviceControl.IsServiceInstalled(_serviceName))
                {
                    if (await _serviceControl.IsServiceRunning(_serviceName, context.GetCancellationToken()))
                    {
                        await _serviceControl.StopService(
                            _serviceName,
                            context.GetCancellationToken());
                    }

                    await _serviceControl.UninstallService(
                        _serviceName);
                }

                await _serviceControl.InstallService(
                    _serviceName,
                    "RKM - Provisioning Monitor",
                    $"{PxeBootSelfLocation.GetSelfLocation()} internal pxeboot monitor-client --provisioner-api-address {context.ParseResult.GetValueForOption(_options.ProvisionerApiAddress)}");

                await _serviceControl.StartService(
                    _serviceName,
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
