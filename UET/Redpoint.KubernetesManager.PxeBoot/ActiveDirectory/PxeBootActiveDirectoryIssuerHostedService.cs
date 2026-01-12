namespace Redpoint.KubernetesManager.PxeBoot.ActiveDirectory
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.Kestrel;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Api;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Tpm;
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Security.Authentication;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    internal class PxeBootActiveDirectoryIssuerHostedService : IHostedService, IKestrelRequestHandler, IAsyncDisposable
    {
        private readonly IKestrelFactory _kestrelFactory;
        private readonly ITpmSecuredHttp _tpmSecuredHttp;
        private readonly IServiceProvider _serviceProvider;
        private readonly PxeBootActiveDirectoryIssuerOptions _options;
        private readonly ICommandInvocationContext _commandInvocationContext;
        private readonly ILogger<PxeBootActiveDirectoryIssuerHostedService> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly IPathResolver _pathResolver;
        private readonly SemaphoreSlim _negotiateSemaphore;

        private ITpmSecuredHttpServer? _tpmSecuredHttpServer;
        private KestrelServer? _kestrelServer;

        public PxeBootActiveDirectoryIssuerHostedService(
            IKestrelFactory kestrelFactory,
            ITpmSecuredHttp tpmSecuredHttp,
            IServiceProvider serviceProvider,
            PxeBootActiveDirectoryIssuerOptions options,
            ICommandInvocationContext commandInvocationContext,
            ILogger<PxeBootActiveDirectoryIssuerHostedService> logger,
            IProcessExecutor processExecutor,
            IPathResolver pathResolver)
        {
            _kestrelFactory = kestrelFactory;
            _tpmSecuredHttp = tpmSecuredHttp;
            _serviceProvider = serviceProvider;
            _options = options;
            _commandInvocationContext = commandInvocationContext;
            _logger = logger;
            _processExecutor = processExecutor;
            _pathResolver = pathResolver;
            _negotiateSemaphore = new SemaphoreSlim(1);
        }

        private const int _httpPort = 8792;
        private const int _httpsPort = 8793;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_kestrelServer != null)
            {
                await _kestrelServer.StopAsync(cancellationToken);
                _kestrelServer.Dispose();
            }

            // @todo: Source certificate authority from somewhere rather than generating it here.
            using var certificateAuthorityPrivateKey = RSA.Create();
            var certificateAuthorityCertificateRequest = new CertificateRequest(
                "CN=RKM AD Issuer Certificate Authority",
                certificateAuthorityPrivateKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            certificateAuthorityCertificateRequest.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    certificateAuthority: true,
                    hasPathLengthConstraint: false,
                    pathLengthConstraint: 0,
                    critical: true));
            var certificateAuthority = certificateAuthorityCertificateRequest.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(3650));

            // @todo: If the certificate authority doesn't change, this also doesn't need to be recreated.
            _tpmSecuredHttpServer = _tpmSecuredHttp.CreateHttpServer(certificateAuthority);

            var kestrelOptions = new KestrelServerOptions();
            kestrelOptions.ApplicationServices = _serviceProvider;
            kestrelOptions.Limits.MaxRequestBodySize = null;
            kestrelOptions.Listen(IPAddress.Any, _httpPort);
            kestrelOptions.Listen(IPAddress.Any, _httpsPort, options =>
            {
                options.UseHttps(https =>
                {
                    _tpmSecuredHttpServer.ConfigureHttps(https);
                });
            });

            _kestrelServer = await _kestrelFactory.CreateAndStartServerAsync(
                kestrelOptions,
                this,
                cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_kestrelServer != null)
            {
                await _kestrelServer.StopAsync(cancellationToken);
                _kestrelServer.Dispose();
                _kestrelServer = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync(CancellationToken.None);

            _negotiateSemaphore.Dispose();
        }

        async Task IKestrelRequestHandler.HandleRequestAsync(HttpContext httpContext)
        {
            if (httpContext.Request.Path == "/negotiate")
            {
                // Handle negotiation.
                await _tpmSecuredHttpServer!.HandleNegotiationRequestAsync(httpContext);
                return;
            }
            else if (httpContext.Request.Path == "/get-join-file")
            {
                GetActiveDirectoryJoinBlobRequest joinRequest;
                try
                {
                    joinRequest = (await JsonSerializer.DeserializeAsync(
                        httpContext.Request.Body,
                        ApiJsonSerializerContext.Default.GetActiveDirectoryJoinBlobRequest))!;
                }
                catch
                {
                    httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }
                if (joinRequest == null ||
                    string.IsNullOrWhiteSpace(joinRequest.NodeName) ||
                    !new Regex("^[a-zA-Z][a-zA-Z0-9-]+$").IsMatch(joinRequest.NodeName))
                {
                    httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                var domain = _commandInvocationContext.ParseResult.GetValueForOption(_options.Domain)!;
                var pem = await _tpmSecuredHttpServer!.GetAikPemVerifiedByClientCertificateAsync(httpContext);
                var fingerprint = RkmNodeFingerprint.CreateFromPem(pem);

                _logger.LogInformation($"Checking if node '{joinRequest.NodeName}' has AIK fingerprint '{fingerprint}' and is authorized...");

                using var httpClient = new HttpClient();
                var checkResponse = await httpClient.PutAsJsonAsync(
                    new Uri($"http://{_commandInvocationContext.ParseResult.GetValueForOption(_options.ProvisionerApiAddress)}:8790/api/check-node-authorized"),
                    new CheckNodeAuthorizedRequest
                    {
                        NodeName = joinRequest.NodeName,
                        AikFingerprint = fingerprint,
                    },
                    ApiJsonSerializerContext.Default.CheckNodeAuthorizedRequest,
                    httpContext.RequestAborted);
                checkResponse.EnsureSuccessStatusCode();
                var checkResponseJson = (await checkResponse.Content.ReadFromJsonAsync(
                    ApiJsonSerializerContext.Default.CheckNodeAuthorizedResponse,
                    httpContext.RequestAborted));
                if (checkResponseJson == null ||
                    !checkResponseJson.Authorized)
                {
                    _logger.LogError($"Node '{joinRequest.NodeName}' with AIK fingerprint '{fingerprint}' is not authorized and can not join Active Directory.");
                    httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return;
                }

                _logger.LogInformation($"Generating join blob for '{joinRequest.NodeName}' on domain '{domain}'...");
                var djoin = await _pathResolver.ResolveBinaryPath("djoin");
                var djoinOutput = new StringBuilder();
                var djoinFilePath = Path.GetTempFileName();
                try
                {
                    var djoinExitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = djoin,
                            Arguments = ["/reuse", "/provision", "/domain", domain, "/machine", joinRequest.NodeName, "/printblob", "/savefile", djoinFilePath]
                        },
                        CaptureSpecification.CreateFromStdoutStringBuilder(djoinOutput),
                        httpContext.RequestAborted);
                    if (djoinExitCode != 0)
                    {
                        _logger.LogError($"djoin failed with non-zero exit code {djoinExitCode}.");
                        httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        return;
                    }
                }
                finally
                {
                    // We don't need this file.
                    File.Delete(djoinFilePath);
                }

                var djoinLines = djoinOutput.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split("\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var djoinBlob = string.Empty;
                for (int i = 0; i < djoinLines.Length; i++)
                {
                    var djoinLine = djoinLines[i];
                    if (djoinLine.StartsWith("Provisioning string (", StringComparison.Ordinal))
                    {
                        djoinBlob = djoinLines[i + 1];
                        break;
                    }
                }
                if (string.IsNullOrWhiteSpace(djoinBlob))
                {
                    _logger.LogError($"djoin did not output in expected format: " + djoinOutput.ToString());
                    httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return;
                }

                if (joinRequest.AsUnattendXml)
                {
                    djoinBlob =
                        $"""
                        <?xml version="1.0" encoding="utf-8"?>
                        <unattend xmlns="urn:schemas-microsoft-com:unattend" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State">
                            <settings pass="offlineServicing">
                                <component name="Microsoft-Windows-UnattendedJoin" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
                                    <OfflineIdentification>
                                        <Provisioning>
                                            <AccountData>{djoinBlob}</AccountData>
                                        </Provisioning>
                                    </OfflineIdentification>
                                </component>
                            </settings>
                        </unattend>
                        """;
                }

                httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
                await JsonSerializer.SerializeAsync(
                    httpContext.Response.Body,
                    new GetActiveDirectoryJoinBlobResponse
                    {
                        JoinBlob = djoinBlob,
                    },
                    ApiJsonSerializerContext.Default.GetActiveDirectoryJoinBlobResponse,
                    httpContext.RequestAborted);
                return;
            }
            else
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
        }
    }
}
