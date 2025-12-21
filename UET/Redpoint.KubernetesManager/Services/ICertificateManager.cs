namespace Redpoint.KubernetesManager.Services
{
    using Redpoint.KubernetesManager.Models;
    using System.Net;

    internal interface ICertificateManager
    {
        string GetCaPublicPemPath();

        Task<ExportedCertificate> GenerateCertificateForAuthorizedNodeAsync(string nodeName, IPAddress ipAddress);

        Task<ExportedCertificate> GenerateCertificateForRequirementAsync(CertificateRequirement requirement);
    }
}
