namespace Redpoint.KubernetesManager.HostedService
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.WindowsServices;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class DefaultHostedServiceFromExecutable : IHostedServiceFromExecutable
    {
        private readonly ILogger<DefaultHostedServiceFromExecutable> _logger;
        private readonly IEnumerable<IHostedService> _hostedServices;
        private readonly RkmHostApplicationLifetime _hostApplicationLifetime;
        private readonly IHostLifetime? _hostLifetime;

        public DefaultHostedServiceFromExecutable(
            ILogger<DefaultHostedServiceFromExecutable> logger,
            IEnumerable<IHostedService> hostedServices,
            RkmHostApplicationLifetime hostApplicationLifetime,
            IHostLifetime? hostLifetime = null)
        {
            _logger = logger;
            _hostedServices = hostedServices;
            _hostApplicationLifetime = hostApplicationLifetime;
            _hostLifetime = hostLifetime;
        }

        public async Task RunHostedServicesAsync(CancellationToken cancellationToken)
        {
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _hostApplicationLifetime.ApplicationStopped,
                _hostApplicationLifetime.ApplicationStopping);

            if (OperatingSystem.IsWindows() && WindowsServiceHelpers.IsWindowsService())
            {
                _logger.LogInformation("Waiting for host lifetime to start...");
                await _hostLifetime!.WaitForStartAsync(cancellationTokenSource.Token);
                _logger.LogInformation("Host lifetime has started.");
            }

            try
            {
                // Wire up Ctrl-C to request stop, to allow start to be cancelled.
                if (cancellationToken.IsCancellationRequested)
                {
                    _hostApplicationLifetime.StopRequestedGate.Open();
                }
                else
                {
                    cancellationToken.Register(_hostApplicationLifetime.StopRequestedGate.Open);
                }
                cancellationToken.ThrowIfCancellationRequested();

                // Start services.
                foreach (var hostedService in _hostedServices)
                {
                    _logger.LogInformation($"Starting hosted service '{hostedService.GetType().FullName}'...");
                    try
                    {
                        await hostedService.StartAsync(cancellationTokenSource.Token);
                        _logger.LogInformation($"Started hosted service '{hostedService.GetType().FullName}'.");
                    }
                    catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
                    {
                        // Application started shutting down during start logic.
                        return;
                    }
                }

                // Tells the service lifetime that we have now started. The service
                // lifetime will call StopApplication when we should shutdown, which
                // will open the gate.
                _hostApplicationLifetime.CtsStarted.Cancel();

                // Wait until shutdown is requested.
                _logger.LogInformation("RKM is waiting for StopRequestedGate to be opened...");
                await _hostApplicationLifetime.StopRequestedGate.WaitAsync(CancellationToken.None);
                _logger.LogInformation("RKM is now shutting down due to StopRequestedGate being opened.");
            }
            finally
            {
                _logger.LogInformation("Shutting down service...");

                if (!_hostApplicationLifetime.CtsStopping.IsCancellationRequested)
                {
                    _hostApplicationLifetime.CtsStopping.Cancel();
                }

                using var cts = new CancellationTokenSource(30000);
                using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cts.Token,
                    _hostApplicationLifetime.ApplicationStopped);

                foreach (var hostedService in _hostedServices)
                {
                    _logger.LogInformation($"Stopping hosted service '{hostedService.GetType().FullName}'...");
                    try
                    {
                        await hostedService.StopAsync(stopCts.Token);
                        _logger.LogInformation($"Stopped hosted service '{hostedService.GetType().FullName}'.");
                    }
                    catch (OperationCanceledException) when (stopCts.Token.IsCancellationRequested)
                    {
                        _logger.LogWarning($"Stop of hosted service '{hostedService.GetType().FullName}' was cancelled due to timeout.");
                    }
                }

                if (!_hostApplicationLifetime.CtsStopped.IsCancellationRequested)
                {
                    _hostApplicationLifetime.CtsStopped.Cancel();
                }

                try
                {
                    if (_hostLifetime != null)
                    {
                        _logger.LogInformation($"Notifying host lifetime of stop...");
                        // @note: This call blocks on _hostApplicationLifetime.ApplicationStopped firing, so it
                        // must occur after we cancel the 'stopped' token for us to signal Windows Services.
                        await _hostLifetime!.StopAsync(cts.Token);
                        _logger.LogInformation($"Notifying host lifetime of stop has completed.");
                    }
                }
                catch
                {
                }

                _logger.LogInformation("Service has been stopped.");
            }
        }
    }
}
