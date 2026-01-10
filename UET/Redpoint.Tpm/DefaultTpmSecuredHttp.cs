namespace Redpoint.Tpm
{
    using Redpoint.KubernetesManager.Tpm.Negotiate;
    using Redpoint.Tpm.Internal;
    using Redpoint.Tpm.Negotiate;
    using System;
    using System.Net.Http.Json;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class DefaultTpmSecuredHttp : ITpmSecuredHttp
    {
        private readonly ITpmService _tpmService;

        public DefaultTpmSecuredHttp(
            ITpmService tpmService)
        {
            _tpmService = tpmService;
        }

        internal static X509Certificate2 ReexportForWindows(X509Certificate2 certificate)
        {
            if (OperatingSystem.IsWindows())
            {
                return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
            }
            return certificate;
        }

        public ITpmSecuredHttpServer CreateHttpServer(
            X509Certificate2 certificateAuthority)
        {
            return new DefaultTpmSecuredHttpServer(
                certificateAuthority,
                _tpmService);
        }

        private (CertificateRequest csr, RSA privateKey) CreatePrivateKeyAndCsrForAik(byte[] aikPublicBytes)
        {
            var parameters = _tpmService.GetRsaParameters(aikPublicBytes);
            var commonName = $"rkm.attestation:{Convert.ToBase64String(parameters.Exponent!)}:{Convert.ToBase64String(parameters.Modulus!)}";

            var distinguishedName = new X500DistinguishedName($"CN=\"{commonName}\"");

            var rsa = RSA.Create(SecurityConstants.RsaKeyBitsCert);

            var request = new CertificateRequest(
                distinguishedName,
                rsa,
                SecurityConstants.RsaHashAlgorithmName,
                SecurityConstants.RsaSignaturePadding);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: true,
                pathLengthConstraint: 0,
                critical: true));

            return (request, rsa);
        }

        public async Task<ITpmSecuredHttpClientFactory> CreateHttpClientFactoryAsync(
            Uri negotiateUrl,
            CancellationToken cancellationToken)
        {
            var (ekPublicBytes, aikPublicBytes, handles) = await _tpmService.CreateRequestAsync();
            string aikPublicPem;
            NegotiateCertificateResponseBundle decryptedBundle;
            X509Certificate2 clientCertificate, certificateAuthority;
            try
            {
                (aikPublicPem, _) = _tpmService.GetPemAndHash(aikPublicBytes);

                var (clientCsr, clientPrivateKey) = CreatePrivateKeyAndCsrForAik(aikPublicBytes);

                var clientCsrPem = clientCsr.CreateSigningRequestPem();

                using var negotiateClient = new HttpClient();

                var response = await negotiateClient.PutAsJsonAsync(
                    negotiateUrl,
                    new NegotiateCertificateRequest
                    {
                        EkTpmPublicBase64 = Convert.ToBase64String(ekPublicBytes),
                        AikTpmPublicBase64 = Convert.ToBase64String(aikPublicBytes),
                        ClientCertificateCsrPem = clientCsrPem,
                    },
                    NegotiateJsonSerializerContext.Default.NegotiateCertificateRequest,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadFromJsonAsync(
                    NegotiateJsonSerializerContext.Default.NegotiateCertificateResponse,
                    cancellationToken);

                var decryptedBundleJson = Encoding.ASCII.GetString(_tpmService.DecryptSecretKey(
                    handles,
                    Convert.FromBase64String(responseJson!.EnvelopingKeyBase64),
                    Convert.FromBase64String(responseJson.EncryptedKeyBase64),
                    Convert.FromBase64String(responseJson.EncryptedBundleJsonBase64)));
                decryptedBundle = JsonSerializer.Deserialize(
                    decryptedBundleJson,
                    NegotiateJsonSerializerContext.Default.NegotiateCertificateResponseBundle)!;

                clientCertificate = X509Certificate2.CreateFromPem(
                    decryptedBundle.ClientSignedPem,
                    clientPrivateKey.ExportRSAPrivateKeyPem());
                certificateAuthority = X509Certificate2.CreateFromPem(
                    decryptedBundle.CertificateAuthorityPem);
            }
            finally
            {
                handles.Dispose();
            }

            // @note: If we wanted to validate the received certificate authority against an
            // external source, this is where it needs to happen.

            return new DefaultTpmSecuredHttpClientFactory(
                certificateAuthority,
                clientCertificate,
                aikPublicPem);
        }
    }
}
