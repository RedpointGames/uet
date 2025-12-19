namespace Redpoint.KubernetesManager.Components
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services.Windows;
    using Redpoint.KubernetesManager.Signalling;
    using System;
    using System.Diagnostics;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.ServiceControl;
    using Redpoint.KubernetesManager.Abstractions;

    /// <summary>
    /// The containerd component sets up and runs the containerd process.
    /// </summary>
    internal class ContainerdComponent : IComponent, IDisposable
    {
        private readonly IServiceControl _serviceControl;
        private readonly IRkmVersionProvider _rkmVersionProvider;
        private readonly IPathProvider _pathProvider;
        private readonly ILogger<ContainerdComponent> _logger;
        private readonly CancellationTokenSource _stoppingToken;
        private Task? _logTask;

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
            _stoppingToken = new CancellationTokenSource();
        }

        public void Dispose()
        {
            ((IDisposable)_stoppingToken).Dispose();
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            context.OnSignal(WellKnownSignals.Stopping, OnStoppingAsync);
        }

        private const string _serviceName = "rkm-containerd";

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);

            Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "cache"));

            var arguments = $"\"{_rkmVersionProvider.UetGenericFilePath}\" cluster run-containerd --manifest-path \"{Path.Combine(_pathProvider.RKMRoot, "cache", "containerd-manifest.json")}\"";

            var installed = false;
            if (await _serviceControl.IsServiceInstalled(_serviceName))
            {
                var result = await _serviceControl.GetServiceExecutableAndArguments(_serviceName);
                if (result == arguments)
                {
                    _logger.LogInformation("containerd service is already installed correctly.");
                    installed = true;
                }
                else
                {
                    if (await _serviceControl.IsServiceRunning(_serviceName, cancellationToken))
                    {
                        _logger.LogInformation("containerd service is being stopped so it can be reinstalled.");
                        await _serviceControl.StopService(_serviceName, cancellationToken);
                    }

                    _logger.LogInformation("containerd service is being uninstalled because the command line arguments need to change.");
                    await _serviceControl.UninstallService(_serviceName);
                }
            }

            if (!installed)
            {
                await _serviceControl.InstallService(
                    _serviceName,
                    "RKM - Containerd",
                    arguments,
                    manualStart: true);
            }

            if (Debugger.IsAttached)
            {
                _logTask = _serviceControl.StreamLogsUntilCancelledAsync(
                    _serviceName,
                    (level, message) =>
                    {
                        switch (level)
                        {
                            case ServiceLogLevel.Information:
                                _logger.LogInformation($"(containerd) {message}");
                                break;
                            case ServiceLogLevel.Warning:
                                _logger.LogWarning($"(containerd) {message}");
                                break;
                            case ServiceLogLevel.Error:
                                _logger.LogError($"(containerd) {message}");
                                break;
                            default:
                                _logger.LogInformation($"(containerd) {message}");
                                break;
                        }
                    },
                    _stoppingToken.Token);
            }

            if (!await _serviceControl.IsServiceRunning(_serviceName, cancellationToken))
            {
                _logger.LogInformation("containerd service is being started...");
                await _serviceControl.StartService(_serviceName, cancellationToken);
            }
        }

        private async Task OnStoppingAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            if (await _serviceControl.IsServiceInstalled(_serviceName))
            {
                _logger.LogInformation("containerd service is being stopped...");
                await _serviceControl.StopService(_serviceName, cancellationToken);
            }

            if (_logTask != null && !_stoppingToken.IsCancellationRequested)
            {
                _stoppingToken.Cancel();

                try
                {
                    await _logTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }
}
