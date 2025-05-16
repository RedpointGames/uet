using Redpoint.KubernetesManager.Models;

namespace Redpoint.KubernetesManager.Services
{
    internal interface IKubeConfigGenerator
    {
        string GenerateKubeConfig(
            string certificateAuthorityPem,
            string primaryNodeAddress,
            ExportedCertificate userCertificate);
    }
}
