namespace Redpoint.KubernetesManager.Tpm
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    internal class DefaultTpmCertificateService : ITpmCertificateService
    {
        private readonly ITpmService _tpmService;

        public DefaultTpmCertificateService(ITpmService tpmService)
        {
            _tpmService = tpmService;
        }

        public (CertificateRequest csr, RSA privateKey) CreatePrivateKeyAndCsrForAik(byte[] aikPublicBytes)
        {
            var parameters = _tpmService.GetRsaParameters(aikPublicBytes);
            var commonName = $"rkm.attestation:{Convert.ToBase64String(parameters.Exponent!)}:{Convert.ToBase64String(parameters.Modulus!)}";

            var distinguishedName = new X500DistinguishedName($"CN=\"{commonName}\"");

            var rsa = RSA.Create(2048);

            var request = new CertificateRequest(
                distinguishedName,
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: true,
                pathLengthConstraint: 0,
                critical: true));

            return (request, rsa);
        }

        public X509Certificate2 SignCsrWithCertificateAuthority(CertificateRequest csr, X509Certificate2 certificateAuthority)
        {
            return csr.Create(
                certificateAuthority,
                DateTimeOffset.UtcNow.AddHours(-1),
                DateTimeOffset.UtcNow.AddHours(1),
                [1, 2, 3, 4]);
        }
    }
}
