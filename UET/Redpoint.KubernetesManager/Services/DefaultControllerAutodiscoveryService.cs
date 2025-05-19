namespace Redpoint.KubernetesManager.Services
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    internal class DefaultControllerAutodiscoveryService : IControllerAutodiscoveryService
    {
        private readonly ILogger<DefaultControllerAutodiscoveryService> _logger;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private Task? _autoDiscoveryServiceTask = null;

        public DefaultControllerAutodiscoveryService(
            ILogger<DefaultControllerAutodiscoveryService> logger,
            ILocalEthernetInfo localEthernetInfo,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _logger = logger;
            _localEthernetInfo = localEthernetInfo;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        public async Task<string?> AttemptAutodiscoveryOfController(CancellationToken stoppingToken)
        {
            using (var udp = new UdpClient())
            {
                _logger.LogInformation("Attempting to auto-discover existing Kubernetes controller on network... (this may take up to 10 seconds)");

                var datagram = Encoding.ASCII.GetBytes("rkm-autodiscovery");

                // Send a few requests since UDP is unreliable. We'll only care about the first response we get back.
                for (var i = 0; i < 10; i++)
                {
                    await udp.SendAsync(datagram, datagram.Length, new IPEndPoint(IPAddress.Broadcast, 8374));
                }

                // Now wait for a response for up to 10 seconds.
                var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var timeoutTask = Task.Run(async () =>
                {
                    await Task.Delay(10 * 1000, timeout.Token);
                    timeout.Cancel();
                }, cancellationToken: stoppingToken);
                try
                {
                    while (!timeout.IsCancellationRequested)
                    {
                        var receivedToken = await udp.ReceiveAsync(timeout.Token);
                        if (_localEthernetInfo.IsLoopbackAddress(receivedToken.RemoteEndPoint.Address))
                        {
                            _logger.LogWarning($"Ignoring auto-discovery response from {receivedToken.RemoteEndPoint.Address} because it is a local address.");
                        }
                        else
                        {
                            _logger.LogInformation($"Auto-discovered Kubernetes controller at: {receivedToken.RemoteEndPoint.Address}");
                            return receivedToken.RemoteEndPoint.Address.ToString();
                        }
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                }
                _logger.LogWarning("Unable to discover existing Kubernetes controller on network!");
                return null;
            }
        }

        public void StartAutodiscovery()
        {
            if (_autoDiscoveryServiceTask == null)
            {
                _autoDiscoveryServiceTask = Task.Run(RunAutodiscoveryAsync);
            }
        }

        private async Task RunAutodiscoveryAsync()
        {
            try
            {
                using var udp = new UdpClient();
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, 8374));
                _logger.LogInformation($"Started auto-discovery service on port {_localEthernetInfo.IPAddress}:8374.");

                while (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    var request = await udp.ReceiveAsync(_hostApplicationLifetime.ApplicationStopping);
                    _logger.LogInformation($"Got message on auto-discovery address from: {request.RemoteEndPoint.Address}");

                    if (Encoding.ASCII.GetString(request.Buffer) == "rkm-autodiscovery")
                    {
                        _logger.LogInformation($"Responding to auto-discovery request from: {request.RemoteEndPoint.Address}");
                        var response = Encoding.ASCII.GetBytes("rkm-announce");
                        for (var i = 0; i < 10; i++)
                        {
                            await udp.SendAsync(response, response.Length, request.RemoteEndPoint);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested || _hostApplicationLifetime.ApplicationStopped.IsCancellationRequested)
            {
                // Expected.
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"Auto-discovery loop unexpectedly failed, which will cause rkm to shutdown as it will no longer be able to respond to new nodes: {ex.Message}");
            }
            finally
            {
                if (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested &&
                    !_hostApplicationLifetime.ApplicationStopped.IsCancellationRequested)
                {
                    Environment.ExitCode = 1;
                    _hostApplicationLifetime.StopApplication();
                }
            }
        }
    }
}
