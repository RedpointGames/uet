using Redpoint.KubernetesManager.Models;

namespace Redpoint.KubernetesManager.Services
{
    internal interface IKubeconfigGenerator
    {
        string GenerateKubeconfig(
            string certificateAuthorityPem,
            string primaryNodeAddress,
            ExportedCertificate userCertificate);

        Task<string> GenerateKubeconfigOnController(
            CertificateRequirement certificateRequirement,
            CancellationToken cancellationToken);
    }
}
