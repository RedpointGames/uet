namespace Redpoint.KubernetesManager.Services
{
    using Microsoft.Extensions.Logging;
    using System.Globalization;
    using System.Reflection;

    internal class DefaultPathProvider : IPathProvider, IDisposable
    {
        private readonly ILogger<DefaultPathProvider> _logger;
        private readonly IRkmVersionProvider _rkmVersionProvider;
        private readonly Lazy<string> _rkmRoot;
        private string? _rkmInstallationId;
        private readonly SemaphoreSlim _semaphoreSlim;

        public DefaultPathProvider(
            ILogger<DefaultPathProvider> logger,
            IRkmVersionProvider rkmVersionProvider)
        {
            _logger = logger;
            _rkmVersionProvider = rkmVersionProvider;
            _rkmRoot = new Lazy<string>(EnsureRKMRootInternal);
            _semaphoreSlim = new SemaphoreSlim(1);
        }

        public void EnsureRKMRoot()
        {
            _ = _rkmRoot.Value;
        }

        private string EnsureRKMRootInternal()
        {
            _semaphoreSlim.Wait();
            try
            {
                string rkmRoot, rkmActiveFile;
                if (OperatingSystem.IsWindows())
                {
                    rkmRoot = @"C:\RKM";
                    rkmActiveFile = @"C:\RKM\active";
                }
                else
                {
                    rkmRoot = "/opt/rkm";
                    rkmActiveFile = "/opt/rkm/active";
                }

                _logger.LogInformation("Ensuring that RKM base directory exists...");
                Directory.CreateDirectory(rkmRoot);

                _logger.LogInformation("Computing RKM installation...");
                var installationId = (Environment.MachineName + "-" + DateTimeOffset.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)).ToLowerInvariant();
                if (!File.Exists(rkmActiveFile) || !Directory.Exists(Path.Combine(rkmRoot, File.ReadAllText(rkmActiveFile).Trim())))
                {
                    File.WriteAllText(rkmActiveFile, installationId);
                }
                else
                {
                    installationId = File.ReadAllText(rkmActiveFile).Trim();
                }
                _logger.LogInformation($"RKM installation ID is: {installationId}");
                _rkmInstallationId = installationId;

                _logger.LogInformation("Ensuring that RKM installation directory exists...");
                var rkmInstall = Path.Combine(rkmRoot, installationId);
                Directory.CreateDirectory(rkmInstall);
                return rkmInstall;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public void Dispose()
        {
            ((IDisposable)_semaphoreSlim).Dispose();
        }

        public string RKMRoot => _rkmRoot.Value;

        public string RKMInstallationId
        {
            get
            {
                _ = _rkmRoot.Value;
                return _rkmInstallationId!;
            }
        }

        public string RKMVersion => _rkmVersionProvider.Version;
    }
}
