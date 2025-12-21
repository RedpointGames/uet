namespace Redpoint.KubernetesManager.Services
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services.Wsl;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;

    internal class DefaultKubeconfigGenerator : IKubeconfigGenerator
    {
        private readonly ICertificateManager _certificateManager;
        private readonly IWslTranslation _wslTranslation;

        public DefaultKubeconfigGenerator(
            ICertificateManager certificateManager,
            IWslTranslation wslTranslation)
        {
            _certificateManager = certificateManager;
            _wslTranslation = wslTranslation;
        }

        public string GenerateKubeconfig(
            string certificateAuthorityPem,
            string primaryNodeAddress,
            ExportedCertificate userCertificate)
        {
            return @$"
apiVersion: v1
kind: Config
clusters:
  - name: kubernetes
    cluster:
      server: https://{primaryNodeAddress}:6443
      certificate-authority-data: {Convert.ToBase64String(Encoding.UTF8.GetBytes(certificateAuthorityPem))}
users:
  - name: default
    user:
      client-certificate-data: {Convert.ToBase64String(Encoding.UTF8.GetBytes(userCertificate.CertificatePem))}
      client-key-data: {Convert.ToBase64String(Encoding.UTF8.GetBytes(userCertificate.PrivateKeyPem))}
contexts:
  - name: default
    context:
      cluster: kubernetes
      name: default
      user: default
current-context: default
".Trim() + "\n";
        }


        public async Task<string> GenerateKubeconfigOnController(
            CertificateRequirement certificateRequirement,
            CancellationToken cancellationToken)
        {
            var primaryNodeAddress = await _wslTranslation.GetTranslatedIPAddress(cancellationToken);

            return GenerateKubeconfig(
                await File.ReadAllTextAsync(_certificateManager.GetCaPublicPemPath(), cancellationToken),
                primaryNodeAddress.ToString(),
                await _certificateManager.GenerateCertificateForRequirementAsync(certificateRequirement));
        }
    }
}
