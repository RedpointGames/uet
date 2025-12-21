namespace Redpoint.KubernetesManager.Services
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.KubernetesManager.Models;
    using System.Net;

    internal class DefaultCertificateManager : ICertificateManager
    {
        private readonly IPathProvider _pathProvider;
        private readonly ICertificateGenerator _certificateGenerator;
        private readonly ILogger<DefaultCertificateManager> _logger;

        public DefaultCertificateManager(
            IPathProvider pathProvider,
            ICertificateGenerator certificateGenerator,
            ILogger<DefaultCertificateManager> logger)
        {
            _pathProvider = pathProvider;
            _certificateGenerator = certificateGenerator;
            _logger = logger;
        }

        public string GetCaPublicPemPath()
        {
            var certsPath = Path.Combine(_pathProvider.RKMRoot, "certs");

            var caPath = Path.Combine(certsPath, "ca");
            var caPemPath = Path.Combine(caPath, "ca.pem");

            return caPemPath;
        }

        public async Task<ExportedCertificate> GenerateCertificateForAuthorizedNodeAsync(string nodeName, IPAddress ipAddress)
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

            _logger.LogInformation($"Generating certificate for authorized node '{nodeName}' from IP address '{ipAddress}'...");

            return _certificateGenerator.GenerateCertificate(
                certificateAuthority,
                requirement.CommonName!,
                requirement.Role!,
                requirement.AdditionalSubjectNames);
        }

        public async Task<ExportedCertificate> GenerateCertificateForRequirementAsync(CertificateRequirement requirement)
        {
            var certsPath = Path.Combine(_pathProvider.RKMRoot, "certs");

            var caPath = Path.Combine(certsPath, "ca");
            var caPemPath = Path.Combine(caPath, "ca.pem");
            var caKeyPath = Path.Combine(caPath, "ca.key");

            var certificateAuthority = new ExportedCertificate(
                (await File.ReadAllTextAsync(caPemPath)).Trim(),
                (await File.ReadAllTextAsync(caKeyPath)).Trim());

            _logger.LogInformation($"Generating certificate for requirement common name '{requirement.CommonName}' and role '{requirement.Role}'...");

            return _certificateGenerator.GenerateCertificate(
                certificateAuthority,
                requirement.CommonName!,
                requirement.Role!,
                requirement.AdditionalSubjectNames);
        }
    }
}
