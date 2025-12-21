using Redpoint.KubernetesManager.Models;
using System.Net;

namespace Redpoint.KubernetesManager.Services
{
    internal interface ICertificateGenerator
    {
        Task<ExportedCertificate> GenerateCertificateAuthorityAsync(CancellationToken cancellationToken);

        ExportedCertificate GenerateCertificate(ExportedCertificate certificateAuthority, string name, string role, string[]? additionalSubjectNames = null);
    }
}
