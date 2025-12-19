namespace Redpoint.KubernetesManager.Services.Wsl
{
    using Redpoint.KubernetesManager.Abstractions;
    using System;
    using System.Net;

    internal class DefaultWslTranslation : IWslTranslation
    {
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IWslDistro _wslDistro;
        private IPAddress? _cachedIPAddress;

        public DefaultWslTranslation(
            ILocalEthernetInfo localEthernetInfo,
            IWslDistro wslDistro)
        {
            _localEthernetInfo = localEthernetInfo;
            _wslDistro = wslDistro;
            _cachedIPAddress = null;
        }

        public string TranslatePath(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                path = path.Replace("\\", "/", StringComparison.Ordinal);
                path = path.Replace("C:/RKM", "/mnt/c/RKM", StringComparison.InvariantCultureIgnoreCase);
                return path;
            }

            return path;
        }

        public async Task<IPAddress> GetTranslatedIPAddress(CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsWindows())
            {
                // WSL processes need to bind to the address of the WSL VM, not the Windows adapter.
                if (_cachedIPAddress == null)
                {
                    _cachedIPAddress = await _wslDistro.GetWslDistroIPAddress(cancellationToken);
                    if (_cachedIPAddress == null)
                    {
                        throw new InvalidOperationException("This function must not be called until OSNetworkingReady flag is raised!");
                    }
                    return _cachedIPAddress;
                }
                return _cachedIPAddress;
            }

            return _localEthernetInfo.IPAddress;
        }

        public string GetTranslatedControllerHostname()
        {
            if (OperatingSystem.IsWindows())
            {
                return $"{Environment.MachineName.ToLowerInvariant()}-wsl";
            }

            return Environment.MachineName.ToLowerInvariant();
        }
    }
}