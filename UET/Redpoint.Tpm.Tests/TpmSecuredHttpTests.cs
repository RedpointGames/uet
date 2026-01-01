namespace Redpoint.Tpm.Tests
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Kestrel;
    using Redpoint.XunitFramework;
    using System;
    using System.Net;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Principal;
    using System.Threading.Tasks;
    using Xunit;

    public sealed class TpmSecuredHttpTests : IKestrelRequestHandler
    {
        private readonly ITestOutputHelper _testOutputHelper;

        private ITpmSecuredHttpServer? _tpmSecuredHttpServer;

        public TpmSecuredHttpTests(ITestOutputHelper testOutputHelper)
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

        [Fact]
        public async Task TestTpmAndCertificates()
        {
            Assert.SkipWhen(Environment.GetEnvironmentVariable("CI") == "true", "TPM is not accessible on GitHub Actions.");
            Assert.SkipUnless(IsAdministrator, "This test can only be run as Administrator, as it requires access to the TPM.");

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(_testOutputHelper);
            });
            serviceCollection.AddTpm();
            serviceCollection.AddKestrelFactory();
            serviceCollection.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var tpmSecuredHttpService = serviceProvider.GetRequiredService<ITpmSecuredHttp>();
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

            _tpmSecuredHttpServer = tpmSecuredHttpService.CreateHttpServer(certificateAuthority);

            var kestrelOptions = new KestrelServerOptions();
            kestrelOptions.ApplicationServices = serviceProvider;
            kestrelOptions.ListenLocalhost(8790);
            kestrelOptions.ListenLocalhost(8791, options =>
            {
                options.UseHttps(https =>
                {
                    _tpmSecuredHttpServer.ConfigureHttps(https);
                });
            });
            using var kestrelServer = await kestrelFactory.CreateAndStartServerAsync(
                kestrelOptions,
                this,
                TestContext.Current.CancellationToken);
            try
            {
                var client = await tpmSecuredHttpService.CreateHttpClientAsync(
                    new Uri("http://127.0.0.1:8790/negotiate"),
                    TestContext.Current.CancellationToken);

                var reflectedPem = await client.GetStringAsync(
                    new Uri("https://127.0.0.1:8791/test"),
                    TestContext.Current.CancellationToken);
                TestContext.Current.AddAttachment("reflectedPem", reflectedPem);
                Assert.NotEmpty(reflectedPem);
            }
            finally
            {
                await kestrelServer.StopAsync(TestContext.Current.CancellationToken);
            }
        }

        async Task IKestrelRequestHandler.HandleRequestAsync(HttpContext httpContext)
        {
            if (httpContext.Request.Path == "/negotiate")
            {
                await _tpmSecuredHttpServer!.HandleNegotiationRequestAsync(httpContext);
            }
            else if (httpContext.Request.Path == "/test")
            {
                var pem = await _tpmSecuredHttpServer!.GetAikPemVerifiedByClientCertificateAsync(httpContext);

                httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
                await httpContext.Response.WriteAsync(pem);
            }
            else
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
        }
    }
}
