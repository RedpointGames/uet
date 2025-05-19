using Redpoint.KubernetesManager.Models;

namespace Redpoint.KubernetesManager.Services
{
    internal interface ICertificateGenerator
    {
        Task<ExportedCertificate> GenerateCertificateAuthorityAsync(CancellationToken cancellationToken);

        ExportedCertificate GenerateCertificate(ExportedCertificate certificateAuthority, string name, string role, string[]? additionalSubjectNames = null);
    }
}
