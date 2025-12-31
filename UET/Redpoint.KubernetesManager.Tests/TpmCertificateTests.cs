namespace Redpoint.KubernetesManager.Tests
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Kestrel;
    using Redpoint.KubernetesManager.HostedService;
    using Redpoint.KubernetesManager.Tpm;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Principal;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public sealed class TpmCertificateTests : IKestrelRequestHandler
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TpmCertificateTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        private static bool IsAdministrator
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    using (var identity = WindowsIdentity.GetCurrent())
                    {
                        var principal = new WindowsPrincipal(identity);
                        return principal.IsInRole(WindowsBuiltInRole.Administrator);
                    }
                }
                return false;
            }
        }

        private static X509Certificate2 ReexportForWindows(X509Certificate2 certificate)
        {
            if (OperatingSystem.IsWindows())
            {
                return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
            }
            return certificate;
        }

        [Fact]
        public async Task TestTpmAndCertificates()
        {
            Assert.SkipUnless(IsAdministrator, "This test can only be run as Administrator, as it requires access to the TPM.");

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(_testOutputHelper);
            });
            serviceCollection.AddRkmTpm();
            serviceCollection.AddKestrelFactory();
            serviceCollection.AddSingleton<IHostApplicationLifetime, RkmHostApplicationLifetime>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var tpmService = serviceProvider.GetRequiredService<ITpmService>();
            var tpmCertificateService = serviceProvider.GetRequiredService<ITpmCertificateService>();
            var kestrelFactory = serviceProvider.GetRequiredService<IKestrelFactory>();

            using var certificateAuthorityPrivateKey = RSA.Create();
            var certificateAuthorityCertificateRequest = new CertificateRequest(
                "CN=Test Issuing Authority",
                certificateAuthorityPrivateKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            certificateAuthorityCertificateRequest.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    certificateAuthority: true,
                    hasPathLengthConstraint: false,
                    pathLengthConstraint: 0,
                    critical: true));
            using var certificateAuthority = certificateAuthorityCertificateRequest.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(1));

            var kestrelOptions = new KestrelServerOptions();
            kestrelOptions.ApplicationServices = serviceProvider;
            kestrelOptions.ListenLocalhost(8791, options =>
            {
                options.UseHttps(https =>
                {
                    https.ServerCertificate = ReexportForWindows(certificateAuthority);
                    https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    https.ClientCertificateValidation = (certificate, chain, policyErrors) =>
                    {
                        using var caChain = new X509Chain();
                        caChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                        caChain.ChainPolicy.CustomTrustStore.Add(certificateAuthority);
                        caChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                        return caChain.Build(certificate);
                    };
                });
            });
            using var kestrelServer = await kestrelFactory.CreateAndStartServerAsync(
                kestrelOptions,
                this,
                TestContext.Current.CancellationToken);
            try
            {
                var (ekPublicBytes, aikPublicBytes, aikContextBytes) = await tpmService.CreateRequestAsync();

                var (clientCsr, clientPrivateKey) = tpmCertificateService.CreatePrivateKeyAndCsrForAik(aikPublicBytes);
                TestContext.Current.AddAttachment("clientPrivatePem", clientPrivateKey.ExportRSAPrivateKeyPem());
                TestContext.Current.AddAttachment("clientPublicPem", clientPrivateKey.ExportRSAPublicKeyPem());
                var clientCsrPem = clientCsr.CreateSigningRequestPem();
                clientCsr = CertificateRequest.LoadSigningRequestPem(
                    clientCsrPem,
                    HashAlgorithmName.SHA256,
                    signerSignaturePadding: RSASignaturePadding.Pkcs1);

                var clientPublicSigned = tpmCertificateService.SignCsrWithCertificateAuthority(clientCsr, certificateAuthority);
                var clientPublicSignedPem = clientPublicSigned.ExportCertificatePem();
                TestContext.Current.AddAttachment("clientPublicPemSigned", clientPublicSignedPem);

                {
                    using var chain = new X509Chain();
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(certificateAuthority);
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    var isVerified = chain.Build(clientPublicSigned);
                    Assert.True(isVerified);
                }

                var (envelopingKey, encryptedKey, encryptedClientPublicSignedPem) = tpmService.Authorize(
                    ekPublicBytes,
                    aikPublicBytes,
                    Encoding.ASCII.GetBytes(clientPublicSignedPem));

                var decryptedClientPublicSignedPem = Encoding.ASCII.GetString(tpmService.DecryptSecretKey(
                    aikContextBytes,
                    envelopingKey,
                    encryptedKey,
                    encryptedClientPublicSignedPem));
                Assert.Equal(clientPublicSignedPem, decryptedClientPublicSignedPem);

                var clientCertificate = X509Certificate2.CreateFromPem(
                    decryptedClientPublicSignedPem,
                    clientPrivateKey.ExportRSAPrivateKeyPem());

                {
                    using var chain = new X509Chain();
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(certificateAuthority);
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    var isVerified = chain.Build(clientCertificate);
                    Assert.True(isVerified);
                }

                var handler = new HttpClientHandler();
                handler.ClientCertificates.Add(ReexportForWindows(clientCertificate));
                handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, policyErrors) =>
                {
                    if (certificate != null && certificate.Thumbprint == certificateAuthority.Thumbprint)
                    {
                        return true;
                    }
                    return false;
                };
                handler.CheckCertificateRevocationList = true;

                using (var client = new HttpClient(handler))
                {
                    var response = await client.GetAsync(
                        new Uri("https://127.0.0.1:8791/"),
                        TestContext.Current.CancellationToken);
                    Assert.True(response.IsSuccessStatusCode);
                }
            }
            finally
            {
                await kestrelServer.StopAsync(TestContext.Current.CancellationToken);
            }
        }

        async Task IKestrelRequestHandler.HandleRequestAsync(HttpContext httpContext)
        {
            var clientCertificate = await httpContext.Connection.GetClientCertificateAsync(httpContext.RequestAborted);
            if (clientCertificate == null)
            {
                httpContext.Response.StatusCode = 401;
            }
            else
            {
                httpContext.Response.StatusCode = 200;
            }
            using (var writer = new StreamWriter(httpContext.Response.Body))
            {
                writer.WriteLine("response");
            }
        }
    }
}
