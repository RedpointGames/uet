namespace Redpoint.KubernetesManager.Manifest.Client
{
    using System.Security.Cryptography.X509Certificates;

    public class GenericManifestSecureConnection
    {
        public required X509Certificate2 CertificateAuthority { get; set; }
        public required string RequiredCommonName { get; set; }
        public required X509Certificate2 ClientCertificate { get; set; }
    }
}
