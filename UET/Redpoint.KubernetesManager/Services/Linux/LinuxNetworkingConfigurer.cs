namespace Redpoint.KubernetesManager.Services
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Models;
    using System;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;

    [SupportedOSPlatform("linux")]
    internal class LinuxNetworkingConfiguration : INetworkingConfiguration
    {
        private readonly ILogger<LinuxNetworkingConfiguration> _logger;
        private readonly IProcessMonitorFactory _singleProcessMonitorFactory;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public LinuxNetworkingConfiguration(
            ILogger<LinuxNetworkingConfiguration> logger,
            IProcessMonitorFactory singleProcessMonitorFactory,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _logger = logger;
            _singleProcessMonitorFactory = singleProcessMonitorFactory;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        public async Task<bool> ConfigureForKubernetesAsync(bool isController, CancellationToken stoppingToken)
        {
            var modprobe = _singleProcessMonitorFactory.CreateTerminatingProcess(new ProcessSpecification(
                filename: "/usr/sbin/modprobe",
                arguments: new[]
                {
                    "br_netfilter"
                }));
            if (await modprobe.RunAsync(stoppingToken) != 0)
            {
                Environment.ExitCode = 1;
                _hostApplicationLifetime.StopApplication();
                _logger.LogCritical("rkm is exiting because it could not modprobe br_netfilter, which is required for networking.");
                return false;
            }

            var enableBridging = _singleProcessMonitorFactory.CreateTerminatingProcess(new ProcessSpecification(
                filename: "/usr/sbin/sysctl",
                arguments: new[]
                {
                    "net.bridge.bridge-nf-call-iptables=1"
                }));
            if (await enableBridging.RunAsync(stoppingToken) != 0)
            {
                Environment.ExitCode = 1;
                _hostApplicationLifetime.StopApplication();
                _logger.LogCritical("rkm is exiting because it could not enable bridging, which is required for networking.");
                return false;
            }

            return true;
        }
    }
}
