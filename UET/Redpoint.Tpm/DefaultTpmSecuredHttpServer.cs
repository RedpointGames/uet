namespace Redpoint.Tpm
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Net.Http.Headers;
    using Redpoint.KubernetesManager.Tpm.Negotiate;
    using Redpoint.Tpm.Internal;
    using Redpoint.Tpm.Negotiate;
    using System;
    using System.Net;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Tpm2Lib;

    internal class DefaultTpmSecuredHttpServer : ITpmSecuredHttpServer
    {
        private readonly X509Certificate2 _certificateAuthority;
        private readonly ITpmService _tpmService;

        public DefaultTpmSecuredHttpServer(
            X509Certificate2 certificateAuthority,
            ITpmService tpmService)
        {
            _certificateAuthority = certificateAuthority;
            _tpmService = tpmService;
        }

        public void ConfigureHttps(HttpsConnectionAdapterOptions https)
        {
            https.ServerCertificate = DefaultTpmSecuredHttp.ReexportForWindows(_certificateAuthority);
            https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            https.ClientCertificateValidation = (certificate, chain, policyErrors) =>
            {
                using var caChain = new X509Chain();
                caChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                caChain.ChainPolicy.CustomTrustStore.Add(_certificateAuthority);
                caChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                return caChain.Build(certificate);
            };
        }

        private static RSAParameters GetParametersFromTpmPublicBase64(string publicBase64)
        {
            var tpmPublicKey = new Marshaller(Convert.FromBase64String(publicBase64))
                .Get<TpmPublic>();

            var rsaParams = (RsaParms)tpmPublicKey.parameters;
            var exponent = rsaParams.exponent != 0 ? Globs.HostToNet(rsaParams.exponent) : RsaParms.DefaultExponent;
            var modulus = (tpmPublicKey.unique as Tpm2bPublicKeyRsa)!.buffer;

            return new RSAParameters
            {
                Exponent = exponent,
                Modulus = modulus,
            };
        }

        public async Task HandleNegotiationRequestAsync(
            HttpContext httpContext)
        {
            if (!string.Equals(httpContext.Request.Method, "PUT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidNegotiationRequestException("Expected a PUT request.");
            }
            if (!httpContext.Request.Headers.TryGetValue("Content-Type", out var contentTypes) ||
                contentTypes.Count != 1 ||
                MediaTypeHeaderValue.Parse(contentTypes[0]).MediaType != "application/json")
            {
                throw new InvalidNegotiationRequestException("Expected content type to be 'application/json'.");
            }

            var request = await JsonSerializer.DeserializeAsync(
                httpContext.Request.Body,
                NegotiateJsonSerializerContext.Default.NegotiateCertificateRequest,
                httpContext.RequestAborted);
            if (request == null)
            {
                throw new InvalidNegotiationRequestException("Failed to deserialize JSON request.");
            }

            var clientCsr = CertificateRequest.LoadSigningRequestPem(
                request.ClientCertificateCsrPem,
                SecurityConstants.RsaHashAlgorithmName,
                signerSignaturePadding: SecurityConstants.RsaSignaturePadding);

            var aikParameters = GetParametersFromTpmPublicBase64(request.AikTpmPublicBase64);
            var commonName = $"rkm.attestation:{Convert.ToBase64String(aikParameters.Exponent!)}:{Convert.ToBase64String(aikParameters.Modulus!)}";
            var expectedDistinguishedName = new X500DistinguishedName($"CN=\"{commonName}\"");
            if (clientCsr.SubjectName.Name != expectedDistinguishedName.Name)
            {
                throw new InvalidNegotiationRequestException($"Client CSR was for name '{clientCsr.SubjectName.Name}', but provided AIK would be name '{expectedDistinguishedName.Name}'.");
            }

            var clientSigned = clientCsr.Create(
                _certificateAuthority,
                DateTimeOffset.UtcNow.AddHours(-1),
                DateTimeOffset.UtcNow.AddHours(1),
                [1, 2, 3, 4]);
            var clientSignedPem = clientSigned.ExportCertificatePem();

            var bundle = new NegotiateCertificateResponseBundle
            {
                CertificateAuthorityPem = _certificateAuthority.ExportCertificatePem(),
                ClientSignedPem = clientSignedPem,
            };
            var bundleJson = JsonSerializer.Serialize(
                bundle,
                NegotiateJsonSerializerContext.Default.NegotiateCertificateResponseBundle);

            var (envelopingKey, encryptedKey, encryptedBundleJson) = _tpmService.Authorize(
                Convert.FromBase64String(request.EkTpmPublicBase64),
                Convert.FromBase64String(request.AikTpmPublicBase64),
                Encoding.ASCII.GetBytes(bundleJson));

            var response = new NegotiateCertificateResponse
            {
                EnvelopingKeyBase64 = Convert.ToBase64String(envelopingKey),
                EncryptedKeyBase64 = Convert.ToBase64String(encryptedKey),
                EncryptedBundleJsonBase64 = Convert.ToBase64String(encryptedBundleJson)
            };

            httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
            httpContext.Response.Headers.Add("Content-Type", "application/json");
            using (var writer = new StreamWriter(httpContext.Response.Body))
            {
                await writer.WriteAsync(
                    JsonSerializer.Serialize(
                        response,
                        NegotiateJsonSerializerContext.Default.NegotiateCertificateResponse));
            }
        }

        public async Task<string> GetAikPemVerifiedByClientCertificateAsync(
            HttpContext httpContext)
        {
            var clientCertificate = await httpContext.Connection.GetClientCertificateAsync();
            if (clientCertificate == null)
            {
                throw new RequestValidationFailedException();
            }

            if (!httpContext.Request.Headers.TryGetValue("RKM-AIK-PEM", out var pemHeader))
            {
                throw new RequestValidationFailedException();
            }
            if (pemHeader.Count != 1)
            {
                throw new RequestValidationFailedException();
            }
            var pemRead = pemHeader[0]!.Replace('|', '\n');

            var aikPublicKey = new RSACryptoServiceProvider();
            aikPublicKey.ImportFromPem(pemRead);
            var aikParameters = aikPublicKey.ExportParameters(false);
            var commonName = $"rkm.attestation:{Convert.ToBase64String(aikParameters.Exponent!)}:{Convert.ToBase64String(aikParameters.Modulus!)}";
            var expectedDistinguishedName = new X500DistinguishedName($"CN=\"{commonName}\"");
            if (clientCertificate.SubjectName.Name != expectedDistinguishedName.Name)
            {
                throw new RequestValidationFailedException();
            }

            return pemRead;
        }
    }
}
