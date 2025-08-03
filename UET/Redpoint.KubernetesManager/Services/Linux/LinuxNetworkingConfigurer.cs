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
        public Task<bool> ConfigureForKubernetesAsync(bool isController, CancellationToken stoppingToken)
        {
            // Now handled via daemon set.
            return Task.FromResult(true);
        }
    }
}
