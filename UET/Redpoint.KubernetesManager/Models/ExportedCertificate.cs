namespace Redpoint.KubernetesManager.Models
{
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    internal class ExportedCertificate
    {
        public ExportedCertificate(string certificatePem, string privateKeyPem)
        {
            CertificatePem = certificatePem;
            PrivateKeyPem = privateKeyPem;
        }

        public ExportedCertificate(X509Certificate2 cert)
        {
            byte[] certificateBytes = cert.RawData;
            char[] certificatePem = PemEncoding.Write("CERTIFICATE", certificateBytes);

            AsymmetricAlgorithm key = cert.GetRSAPrivateKey()!;
            byte[] privKeyBytes = key.ExportPkcs8PrivateKey();
            char[] privKeyPem = PemEncoding.Write("PRIVATE KEY", privKeyBytes);

            CertificatePem = new string(certificatePem);
            PrivateKeyPem = new string(privKeyPem);

            // @note: Kubernetes components do not like having a copy of the root CA certificate
            // included in the certificate file ("expected 1 certificate, found 2"). Since all the
            // Kubernetes components are separately provided a copy of the CA, this is unnecessary
            // anyway.
        }

        public string CertificatePem { get; }

        public string PrivateKeyPem { get; }

        public X509Certificate2 ToCertificate()
        {
            return X509Certificate2.CreateFromPem(
                CertificatePem,
                PrivateKeyPem);
        }
    }
}
