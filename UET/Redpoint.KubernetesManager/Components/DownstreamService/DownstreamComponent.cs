namespace Redpoint.KubernetesManager.Components.DownstreamService
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.ServiceControl;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    internal abstract class DownstreamComponent : IComponent, IDisposable
    {
        private readonly IServiceControl _serviceControl;
        private readonly IRkmVersionProvider _rkmVersionProvider;
        private readonly IPathProvider _pathProvider;
        private readonly ILogger _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly CancellationTokenSource _stoppingToken;
        private Task? _logTask;
        private Task? _monitorTask;

        public DownstreamComponent(
            IServiceControl serviceControl,
            IRkmVersionProvider rkmVersionProvider,
            IPathProvider pathProvider,
            ILogger logger,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _serviceControl = serviceControl;
            _rkmVersionProvider = rkmVersionProvider;
            _pathProvider = pathProvider;
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
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

        protected abstract string ServiceName { get; }

        protected abstract string ServiceDescription { get; }

        protected abstract string RunCommand { get; }

        protected abstract string ManifestFileName { get; }

        protected abstract string DisplayName { get; }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);

            Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "cache"));

            var arguments = $"\"{_rkmVersionProvider.UetGenericFilePath}\" cluster {RunCommand} --manifest-path \"{Path.Combine(_pathProvider.RKMRoot, "cache", ManifestFileName)}\"";

            var installed = false;
            if (await _serviceControl.IsServiceInstalled(ServiceName))
            {
                var result = await _serviceControl.GetServiceExecutableAndArguments(ServiceName);
                if (result == arguments)
                {
                    _logger.LogInformation($"{DisplayName} service is already installed correctly.");
                    installed = true;
                }
                else
                {
                    if (await _serviceControl.IsServiceRunning(ServiceName, cancellationToken))
                    {
                        _logger.LogInformation($"{DisplayName} service is being stopped so it can be reinstalled.");
                        await _serviceControl.StopService(ServiceName, cancellationToken);
                    }

                    _logger.LogInformation($"{DisplayName} service is being uninstalled because the command line arguments need to change.");
                    await _serviceControl.UninstallService(ServiceName);
                }
            }

            if (!installed)
            {
                await _serviceControl.InstallService(
                    ServiceName,
                    ServiceDescription,
                    arguments,
                    manualStart: true);
            }

            if (Debugger.IsAttached)
            {
                _logTask = _serviceControl.StreamLogsUntilCancelledAsync(
                    ServiceName,
                    (level, message) =>
                    {
                        switch (level)
                        {
                            case ServiceLogLevel.Information:
                                _logger.LogInformation($"({DisplayName}) {message}");
                                break;
                            case ServiceLogLevel.Warning:
                                _logger.LogWarning($"({DisplayName}) {message}");
                                break;
                            case ServiceLogLevel.Error:
                                _logger.LogError($"({DisplayName}) {message}");
                                break;
                            default:
                                _logger.LogInformation($"({DisplayName}) {message}");
                                break;
                        }
                    },
                    _stoppingToken.Token);
            }

            if (!await _serviceControl.IsServiceRunning(ServiceName, cancellationToken))
            {
                _logger.LogInformation($"{DisplayName} service is being started...");
                await _serviceControl.StartService(ServiceName, cancellationToken);
            }

            _monitorTask = Task.Run(
                async () =>
                {
                    try
                    {
                        while (!_stoppingToken.Token.IsCancellationRequested)
                        {
                            // Check every 5 minutes.
                            await Task.Delay(5 * 60 * 1000, _stoppingToken.Token);

                            if (!await _serviceControl.IsServiceRunning(ServiceName, _stoppingToken.Token))
                            {
                                _logger.LogWarning($"{DisplayName} service has unexpectedly stopped, restarting...");
                                await _serviceControl.StartService(ServiceName, _stoppingToken.Token);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (_stoppingToken.Token.IsCancellationRequested)
                    {
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Exception in {DisplayName} service monitoring loop: {ex.Message}");

                        _logger.LogError("RKM will be stopped because it encountered an unrecoverable error.");
                        _hostApplicationLifetime.StopApplication();
                    }
                },
                _stoppingToken.Token);
        }

        private async Task OnStoppingAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            if (!_stoppingToken.IsCancellationRequested)
            {
                _stoppingToken.Cancel();
            }

            if (_monitorTask != null)
            {
                try
                {
                    await _monitorTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            if (await _serviceControl.IsServiceInstalled(ServiceName))
            {
                _logger.LogInformation($"{DisplayName} service is being stopped...");
                await _serviceControl.StopService(ServiceName, cancellationToken);
            }

            if (_logTask != null)
            {
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
