namespace Redpoint.KubernetesManager.Components
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Windows;
    using Redpoint.KubernetesManager.Signalling;
    using System;
    using System.Diagnostics;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.ServiceControl;

    /// <summary>
    /// The containerd component sets up and runs the containerd process.
    /// </summary>
    internal class ContainerdComponent : IComponent
    {
        private readonly IServiceControl _serviceControl;
        private readonly IRkmVersionProvider _rkmVersionProvider;
        private readonly IPathProvider _pathProvider;
        private readonly ILogger<ContainerdComponent> _logger;

        public ContainerdComponent(
            IServiceControl serviceControl,
            IRkmVersionProvider rkmVersionProvider,
            IPathProvider pathProvider,
            ILogger<ContainerdComponent> logger)
        {
            _serviceControl = serviceControl;
            _rkmVersionProvider = rkmVersionProvider;
            _pathProvider = pathProvider;
            _logger = logger;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            context.OnSignal(WellKnownSignals.Stopping, OnStoppingAsync);
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            var serviceName = OperatingSystem.IsWindows() ? "RKM - Containerd" : "rkm-containerd";

            Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "cache"));

            var arguments = $"\"{_rkmVersionProvider.UetFilePath}\" cluster run-containerd --manifest-path \"{Path.Combine(_pathProvider.RKMRoot, "cache", "containerd-manifest.json")}\"";

            var installed = false;
            if (await _serviceControl.IsServiceInstalled(serviceName))
            {
                var result = await _serviceControl.GetServiceExecutableAndArguments(serviceName);
                if (result == arguments)
                {
                    _logger.LogInformation("containerd service is already installed correctly.");
                    installed = true;
                }
                else
                {
                    if (await _serviceControl.IsServiceRunning(serviceName))
                    {
                        _logger.LogInformation("containerd service is being stopped so it can be reinstalled.");
                        await _serviceControl.StopService(serviceName);
                    }

                    _logger.LogInformation("containerd service is being uninstalled because the command line arguments need to change.");
                    await _serviceControl.UninstallService(serviceName);
                }
            }

            if (!installed)
            {
                await _serviceControl.InstallService(
                    serviceName,
                    "Runs containerd for RKM.",
                    arguments,
                    manualStart: true);
            }

            if (!await _serviceControl.IsServiceRunning(serviceName))
            {
                _logger.LogInformation("containerd service is being started...");
                await _serviceControl.StartService(serviceName);
            }
        }

        private async Task OnStoppingAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            var serviceName = OperatingSystem.IsWindows() ? "RKM - Containerd" : "rkm-containerd";

            if (await _serviceControl.IsServiceInstalled(serviceName))
            {
                _logger.LogInformation("containerd service is being stopped...");
                await _serviceControl.StopService(serviceName);
            }
        }
    }
}
