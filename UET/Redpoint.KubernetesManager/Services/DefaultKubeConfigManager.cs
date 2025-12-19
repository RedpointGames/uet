namespace Redpoint.KubernetesManager.Services
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services.Wsl;

    internal class DefaultKubeConfigManager : IKubeConfigManager, IDisposable
    {
        private readonly ILogger<DefaultKubeConfigManager> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly IKubeConfigGenerator _kubeConfigGenerator;
        private readonly ICertificateManager _certificateManager;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IWslTranslation _wslTranslation;
        private readonly SemaphoreSlim _generatingSemaphore;

        public DefaultKubeConfigManager(
            ILogger<DefaultKubeConfigManager> logger,
            IPathProvider pathProvider,
            IKubeConfigGenerator kubeConfigGenerator,
            ICertificateManager certificateManager,
            ILocalEthernetInfo localEthernetInfo,
            IWslTranslation wslTranslation)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _kubeConfigGenerator = kubeConfigGenerator;
            _certificateManager = certificateManager;
            _localEthernetInfo = localEthernetInfo;
            _wslTranslation = wslTranslation;
            _generatingSemaphore = new SemaphoreSlim(1);
        }

        public string GetKubeconfigPath(string category, string name)
        {
            return Path.Combine(_pathProvider.RKMRoot, "kubeconfigs", category, name + ".kubeconfig");
        }

        public async Task<string> EnsureGeneratedForNodeAsync(string certificateAuthorityPem, string nodeName)
        {
            await _generatingSemaphore.WaitAsync();
            try
            {
                var kubeconfig = $"nodes/node-{nodeName}";
                var kubeconfigsPath = Path.Combine(_pathProvider.RKMRoot, "kubeconfigs");
                var kubeconfigPath = Path.Combine(kubeconfigsPath, kubeconfig + ".kubeconfig");
                if (!File.Exists(kubeconfigPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(kubeconfigPath)!);
                    _logger.LogInformation($"Generating kubeconfig: {kubeconfig}");
                    var split = kubeconfig.Split('/');
                    var kubeconfigContent = _kubeConfigGenerator.GenerateKubeConfig(
                        certificateAuthorityPem,
                        (await _wslTranslation.GetTranslatedIPAddress(CancellationToken.None)).ToString(),
                        new ExportedCertificate(
                            await File.ReadAllTextAsync(_certificateManager.GetCertificatePemPath(split[0], split[1])),
                            await File.ReadAllTextAsync(_certificateManager.GetCertificateKeyPath(split[0], split[1]))));
                    await File.WriteAllTextAsync(
                        kubeconfigPath,
                        kubeconfigContent);
                    return kubeconfigContent;
                }
                else
                {
                    _logger.LogInformation($"Kubeconfig already exists: {kubeconfig}");
                    return await File.ReadAllTextAsync(kubeconfigPath);
                }
            }
            finally
            {
                _generatingSemaphore.Release();
            }
        }

        public void Dispose()
        {
            ((IDisposable)_generatingSemaphore).Dispose();
        }
    }
}
