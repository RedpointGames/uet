namespace Redpoint.KubernetesManager.Services
{
    using Redpoint.KubernetesManager.Models;
    using System.Net;

    internal interface ICertificateManager
    {
        string GetCertificatePemPath(string category, string name);

        string GetCertificateKeyPath(string category, string name);

        Task<ExportedCertificate> EnsureGeneratedForNodeAsync(string nodeName, IPAddress ipAddress);
    }
}
