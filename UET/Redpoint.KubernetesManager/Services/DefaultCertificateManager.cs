namespace Redpoint.KubernetesManager.Services
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Models;
    using System.Net;

    internal class DefaultCertificateManager : ICertificateManager, IDisposable
    {
        private readonly ILogger<DefaultCertificateManager> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly ICertificateGenerator _certificateGenerator;
        private readonly SemaphoreSlim _generatingSemaphore;

        public DefaultCertificateManager(
            ILogger<DefaultCertificateManager> logger,
            IPathProvider pathProvider,
            ICertificateGenerator certificateGenerator)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _certificateGenerator = certificateGenerator;
            _generatingSemaphore = new SemaphoreSlim(1);
        }

        public async Task<ExportedCertificate> EnsureGeneratedForNodeAsync(string nodeName, IPAddress ipAddress)
        {
            await _generatingSemaphore.WaitAsync();
            try
            {
                var certsPath = Path.Combine(_pathProvider.RKMRoot, "certs");

                var caPath = Path.Combine(certsPath, "ca");
                var caPemPath = Path.Combine(caPath, "ca.pem");
                var caKeyPath = Path.Combine(caPath, "ca.key");

                var certificateAuthority = new ExportedCertificate(
                    (await File.ReadAllTextAsync(caPemPath)).Trim(),
                    (await File.ReadAllTextAsync(caKeyPath)).Trim());

                var requirement = new CertificateRequirement
                {
                    Category = "nodes",
                    FilenameWithoutExtension = $"node-{nodeName}",
                    CommonName = $"system:node:{nodeName}",
                    Role = "system:nodes",
                    AdditionalSubjectNames = new[]
                    {
                        nodeName,
                        ipAddress.ToString()
                    }
                };

                var path = Path.Combine(certsPath, requirement.Category!);
                var pemPath = Path.Combine(path, $"{requirement.FilenameWithoutExtension}.pem");
                var keyPath = Path.Combine(path, $"{requirement.FilenameWithoutExtension}.key");

                if (!File.Exists(pemPath) || !File.Exists(keyPath))
                {
                    _logger.LogInformation($"Generating certificate: {requirement.Category}/{requirement.FilenameWithoutExtension}");
                    var certificate = _certificateGenerator.GenerateCertificate(
                        certificateAuthority,
                        requirement.CommonName!,
                        requirement.Role!,
                        requirement.AdditionalSubjectNames);
                    Directory.CreateDirectory(path);
                    await File.WriteAllTextAsync(pemPath, certificate.CertificatePem);
                    await File.WriteAllTextAsync(keyPath, certificate.PrivateKeyPem);
                    return certificate;
                }
                else
                {
                    _logger.LogInformation($"Certificate already exists: {requirement.Category}/{requirement.FilenameWithoutExtension}");
                    return new ExportedCertificate(
                        await File.ReadAllTextAsync(pemPath),
                        await File.ReadAllTextAsync(keyPath));
                }
            }
            finally
            {
                _generatingSemaphore.Release();
            }
        }

        public string GetCertificatePemPath(string category, string name)
        {
            return Path.Combine(_pathProvider.RKMRoot, "certs", category, $"{name}.pem");
        }

        public string GetCertificateKeyPath(string category, string name)
        {
            return Path.Combine(_pathProvider.RKMRoot, "certs", category, $"{name}.key");
        }

        public void Dispose()
        {
            ((IDisposable)_generatingSemaphore).Dispose();
        }
    }
}
