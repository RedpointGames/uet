namespace Redpoint.KubernetesManager.Components
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using Redpoint.ServiceControl;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    /// <summary>
    /// The kubelet component sets up and runs the kubelet process.
    /// </summary>
    internal class KubeletComponent : IComponent, IDisposable
    {
        private readonly IServiceControl _serviceControl;
        private readonly IRkmVersionProvider _rkmVersionProvider;
        private readonly IPathProvider _pathProvider;
        private readonly ILogger<ContainerdComponent> _logger;
        private readonly CancellationTokenSource _stoppingToken;
        private Task? _logTask;

        public KubeletComponent(
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

        private const string _serviceName = "rkm-kubelet";

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);

            Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "cache"));

            var arguments = $"\"{_rkmVersionProvider.UetFilePath}\" cluster run-kubelet --manifest-path \"{Path.Combine(_pathProvider.RKMRoot, "cache", "kubelet-manifest.json")}\"";

            var installed = false;
            if (await _serviceControl.IsServiceInstalled(_serviceName))
            {
                var result = await _serviceControl.GetServiceExecutableAndArguments(_serviceName);
                if (result == arguments)
                {
                    _logger.LogInformation("kubelet service is already installed correctly.");
                    installed = true;
                }
                else
                {
                    if (await _serviceControl.IsServiceRunning(_serviceName, cancellationToken))
                    {
                        _logger.LogInformation("kubelet service is being stopped so it can be reinstalled.");
                        await _serviceControl.StopService(_serviceName, cancellationToken);
                    }

                    _logger.LogInformation("kubelet service is being uninstalled because the command line arguments need to change.");
                    await _serviceControl.UninstallService(_serviceName);
                }
            }

            if (!installed)
            {
                await _serviceControl.InstallService(
                    _serviceName,
                    "RKM - Kubelet",
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
                                _logger.LogInformation($"(kubelet) {message}");
                                break;
                            case ServiceLogLevel.Warning:
                                _logger.LogWarning($"(kubelet) {message}");
                                break;
                            case ServiceLogLevel.Error:
                                _logger.LogError($"(kubelet) {message}");
                                break;
                            default:
                                _logger.LogInformation($"(kubelet) {message}");
                                break;
                        }
                    },
                    _stoppingToken.Token);
            }

            if (!await _serviceControl.IsServiceRunning(_serviceName, cancellationToken))
            {
                _logger.LogInformation("kubelet service is being started...");
                await _serviceControl.StartService(_serviceName, cancellationToken);
            }
        }

        private async Task OnStoppingAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            if (await _serviceControl.IsServiceInstalled(_serviceName))
            {
                _logger.LogInformation("kubelet service is being stopped...");
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
