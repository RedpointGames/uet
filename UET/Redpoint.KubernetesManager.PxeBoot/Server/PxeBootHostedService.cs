namespace Redpoint.KubernetesManager.PxeBoot.Server
{
    using GitHub.JPMikkers.Dhcp;
    using k8s;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Redpoint.CommandLine;
    using Redpoint.Kestrel;
    using Redpoint.KubernetesManager.Configuration.Json;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Api;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.Tpm;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Tftp.Net;

    internal class PxeBootHostedService : IHostedService, IDisposable, IDhcpMessageInterceptor, IKestrelRequestHandler
    {
        private readonly ILogger<PxeBootHostedService> _logger;
        private readonly IDhcpServerFactory _dhcpServerFactory;
        private readonly PxeBootServerOptions _options;
        private readonly ICommandInvocationContext _commandInvocationContext;
        private readonly IKestrelFactory _kestrelFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly ITpmSecuredHttp _tpmSecuredHttp;
        private readonly IServiceProvider _serviceProvider;
        private readonly List<IProvisioningStep> _provisioningSteps;
        private readonly KubernetesRkmJsonSerializerContext _jsonSerializerContext;

        private IRkmConfigurationSource? _rkmConfigurationSource;
        private TftpServer? _tftpServer;
        private IDhcpServer? _dhcpServer;
        private KestrelServer? _kestrelServer;
        private ITpmSecuredHttpServer? _tpmSecuredHttpServer;

        public PxeBootHostedService(
            ILogger<PxeBootHostedService> logger,
            IDhcpServerFactory dhcpServerFactory,
            PxeBootServerOptions options,
            ICommandInvocationContext commandInvocationContext,
            IKestrelFactory kestrelFactory,
            IEnumerable<IProvisioningStep> provisioningSteps,
            ILoggerFactory loggerFactory,
            IHostApplicationLifetime hostApplicationLifetime,
            ITpmSecuredHttp tpmSecuredHttp,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _dhcpServerFactory = dhcpServerFactory;
            _options = options;
            _commandInvocationContext = commandInvocationContext;
            _kestrelFactory = kestrelFactory;
            _loggerFactory = loggerFactory;
            _hostApplicationLifetime = hostApplicationLifetime;
            _tpmSecuredHttp = tpmSecuredHttp;
            _serviceProvider = serviceProvider;
            _provisioningSteps = provisioningSteps.ToList();

            _jsonSerializerContext = new KubernetesRkmJsonSerializerContext(new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonStringEnumConverter(),
                    new RkmNodeProvisionerStepJsonConverter(provisioningSteps),
                    new KubernetesDateTimeOffsetConverter(),
                }
            });
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            switch (_commandInvocationContext.ParseResult.GetValueForOption(_options.Source))
            {
                case PxeBootServerSource.Test:
                    _rkmConfigurationSource = new TestRkmConfigurationSource(
                        _loggerFactory.CreateLogger<TestRkmConfigurationSource>());
                    break;
                case PxeBootServerSource.KubernetesDefault:
                    _rkmConfigurationSource = new KubernetesRkmConfigurationSource(
                        new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig()));
                    break;
                case PxeBootServerSource.KubernetesInCluster:
                    _rkmConfigurationSource = new KubernetesRkmConfigurationSource(
                        new Kubernetes(KubernetesClientConfiguration.InClusterConfig()));
                    break;
                default:
                    _logger.LogError("Unsupported configuration source.");
                    _hostApplicationLifetime.StopApplication();
                    return;
            }

            if (!File.Exists("ipxe.efi"))
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        _logger.LogInformation("Downloading ipxe.efi...");
                        var stream = await client.GetStreamAsync(new Uri("https://boot.ipxe.org/ipxe.efi"), cancellationToken);
                        using (var target = new FileStream(@"ipxe.efi", FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await stream.CopyToAsync(target, cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    File.Delete("ipxe.efi");
                }
            }

            _tftpServer?.Dispose();

            _tftpServer = new TftpServer(IPAddress.Any);
            _tftpServer.OnReadRequest += OnTftpReadRequest;
            _tftpServer.OnWriteRequest += OnTftpWriteRequest;
            _tftpServer.OnError += OnTftpError;
            _tftpServer.Start();
            _logger.LogInformation("TFTP server started.");

            _dhcpServer?.Dispose();

            var dhcpServerInterfaceName = _commandInvocationContext.ParseResult.GetValueForOption(_options.DhcpOnInterface);
            if (!string.IsNullOrWhiteSpace(dhcpServerInterfaceName))
            {
                string? foundPhysicalNetworkInterface = null;
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var networkInterface in networkInterfaces)
                {
                    if (networkInterface.Name.Contains("RKM-DHCP", StringComparison.Ordinal))
                    {
                        _logger.LogInformation($"Discovered RKM-DHCP network interface with physical address '{networkInterface.GetPhysicalAddress().ToString()}'.");
                        foundPhysicalNetworkInterface = networkInterface.GetPhysicalAddress().ToString();
                        break;
                    }
                }
                if (!string.IsNullOrWhiteSpace(foundPhysicalNetworkInterface))
                {
                    _dhcpServer = _dhcpServerFactory.Create();
                    _dhcpServer.EndPoint = new IPEndPoint(IPAddress.Any, 67);
                    _dhcpServer.NetworkPrefix = IPAddress.Parse("192.168.0.0");
                    _dhcpServer.ServerAddress = IPAddress.Parse("192.168.0.1");
                    _dhcpServer.SubnetMask = IPAddress.Parse("255.255.255.0");
                    _dhcpServer.PoolStart = IPAddress.Parse("192.168.0.100");
                    _dhcpServer.PoolEnd = IPAddress.Parse("192.168.0.200");
                    _dhcpServer.LeaseTime = TimeSpan.FromSeconds(3600);
                    _dhcpServer.OfferExpirationTime = TimeSpan.FromSeconds(3600);
                    _dhcpServer.Interceptors.Add(this);
                    _dhcpServer.Reservations.Add(new ReservationItem
                    {
                        MacTaste = foundPhysicalNetworkInterface,
                        PoolStart = IPAddress.Parse("192.168.0.1"),
                        PoolEnd = IPAddress.Parse("192.168.0.1"),
                        Preempt = true,
                    });
                    _dhcpServer.Start();
                }
                else
                {
                    _logger.LogWarning("Can't reserve 192.168.0.1 for this server on DHCP network interface because it can't be found.");
                }
            }

            if (_kestrelServer != null)
            {
                await _kestrelServer.StopAsync(cancellationToken);
                _kestrelServer.Dispose();
            }

            // @todo: Source certificate authority from somewhere rather than generating it here.
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
            var certificateAuthority = certificateAuthorityCertificateRequest.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(1));

            // @todo: If the certificate authority doesn't change, this also doesn't need to be recreated.
            _tpmSecuredHttpServer = _tpmSecuredHttp.CreateHttpServer(certificateAuthority);

            var kestrelOptions = new KestrelServerOptions();
            kestrelOptions.ApplicationServices = _serviceProvider;
            kestrelOptions.ListenAnyIP(8790);
            kestrelOptions.ListenAnyIP(8791, options =>
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

        private void OnTftpError(TftpTransferError error)
        {
            _logger.LogError($"TFTP error: {error}");
        }

        private void OnTftpWriteRequest(ITftpTransfer transfer, EndPoint client)
        {
            transfer.Cancel(TftpErrorPacket.AccessViolation);
        }

        private void OnTftpReadRequest(ITftpTransfer transfer, EndPoint client)
        {
            try
            {
                _logger.LogInformation($"Got TFTP request from {client} {client.GetType().FullName} for '{transfer.Filename}'");

                transfer.OnProgress += (sender, args) =>
                {
                    _logger.LogTrace($"Transfer progress: {args.TransferredBytes} / {args.TotalBytes}");
                };
                transfer.OnFinished += (sender) =>
                {
                    _logger.LogInformation($"Transfer finished.");
                };
                transfer.OnError += (sender, args) =>
                {
                    _logger.LogInformation($"Transfer error: {args}");
                };

                if (transfer.Filename.TrimStart('/') == "ipxe.efi")
                {
                    _logger.LogInformation($"Transferring ipxe.efi...");
                    transfer.Start(new FileStream(@"ipxe.efi", FileMode.Open, FileAccess.Read, FileShare.Read));
                }
                else if (transfer.Filename.TrimStart('/') == "autoexec.ipxe")
                {
                    _logger.LogInformation($"Transferring autoexec.ipxe...");
                    var stream = new MemoryStream();
                    using (var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true))
                    {
                        var ipEndpoint = (IPEndPoint)client;
                        // @todo: We probably need to modify Tftp.Net to work async...
                        writer.Write(GetAutoexecScript(ipEndpoint.Address, CancellationToken.None).Result);
                    }
                    stream.Seek(0, SeekOrigin.Begin);
                    transfer.Start(stream);
                }
                else
                {
                    transfer.Cancel(TftpErrorPacket.FileNotFound);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                transfer.Cancel(TftpErrorPacket.IllegalOperation);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _tftpServer?.Dispose();
            _tftpServer = null;

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _tftpServer?.Dispose();
            _tftpServer = null;

            _dhcpServer?.Dispose();
            _dhcpServer = null;

            _kestrelServer?.Dispose();
            _kestrelServer = null;
        }

        void IDhcpMessageInterceptor.Apply(DhcpMessage sourceMsg, DhcpMessage targetMsg)
        {
            var vendorClientMatch = sourceMsg.FindOption<DhcpOptionVendorClassIdentifier>();
            if (vendorClientMatch != null)
            {
                var vendorClient = Encoding.ASCII.GetString(vendorClientMatch?.Data ?? []).Trim();
                if (vendorClient.StartsWith("HTTPClient", StringComparison.Ordinal))
                {
                    _logger.LogInformation("DHCP boot from HTTPClient...");
                    targetMsg.Options.Add(new DhcpOptionBootFileName("http://192.168.0.1:8790/ipxe.efi"));
                }
                else if (vendorClient.StartsWith("PXEClient", StringComparison.Ordinal))
                {
                    _logger.LogInformation("DHCP boot from PXEClient...");
                    targetMsg.Options.Add(new DhcpOptionTftpServerName("192.168.0.1"));
                    targetMsg.Options.Add(new DhcpOptionBootFileName("ipxe.efi"));
                    targetMsg.NextServerIPAddress = IPAddress.Parse("192.168.0.1");
                }
            }
        }

        private class IpxeProvisioningStepServerContext : IProvisioningStepServerContext
        {
            private readonly IPAddress _remoteIpAddress;

            public IpxeProvisioningStepServerContext(IPAddress remoteIpAddress)
            {
                _remoteIpAddress = remoteIpAddress;
            }

            public IPAddress RemoteIpAddress => _remoteIpAddress;
        }

        private async Task<string> GetAutoexecScript(IPAddress sourceIpAddress, CancellationToken cancellationToken)
        {
            var defaultScript =
                """
                #!ipxe
                dhcp
                shell
                """;

            var node = await _rkmConfigurationSource!.GetRkmNodeByRegisteredIpAddressAsync(
                sourceIpAddress.ToString(),
                cancellationToken);
            if (node == null)
            {
                return defaultScript;
            }
            if (string.IsNullOrWhiteSpace(node?.Status?.Provisioner?.Name))
            {
                return defaultScript;
            }
            var provisioner = await _rkmConfigurationSource.GetRkmNodeProvisionerAsync(
                node.Status.Provisioner.Name,
                _jsonSerializerContext.RkmNodeProvisionerSpec,
                cancellationToken);
            if (provisioner == null ||
                (provisioner.GetHash(_jsonSerializerContext.RkmNodeProvisioner) != node.Status.Provisioner.Hash && !string.IsNullOrWhiteSpace(node.Status.Provisioner.Hash)) ||
                (provisioner.Spec?.Steps?.Count ?? 0) <= (node.Status.Provisioner.CurrentStepIndex ?? 0))
            {
                return defaultScript;
            }

            var serverContext = new IpxeProvisioningStepServerContext(sourceIpAddress);

            var currentStep = provisioner.Spec!.Steps![node.Status.Provisioner.CurrentStepIndex!.Value];
            var provisioningStep = _provisioningSteps.First(x => string.Equals(x.Type, currentStep?.Type, StringComparison.OrdinalIgnoreCase));

            var selectedScript = await provisioningStep.GetIpxeAutoexecScriptOverrideOnServerUncastedAsync(
                currentStep!.DynamicSettings,
                node.Status,
                serverContext,
                cancellationToken);

            if (provisioningStep.Flags.HasFlag(ProvisioningStepFlags.AssumeCompleteWhenIpxeScriptFetched))
            {
                await provisioningStep.ExecuteOnServerUncastedAfterAsync(
                    currentStep!.DynamicSettings,
                    node.Status,
                    serverContext,
                    cancellationToken);

                // Increment current step index and then update node status.
                if (!node.Status.Provisioner.CurrentStepIndex.HasValue)
                {
                    node.Status.Provisioner.CurrentStepIndex = 0;
                }
                node.Status.Provisioner.CurrentStepIndex += 1;
                if (node.Status.Provisioner.CurrentStepIndex >= provisioner.Spec.Steps.Count)
                {
                    node.Status.Provisioner = null;
                }
                else
                {
                    node.Status.Provisioner.CurrentStepStarted = !provisioningStep.Flags.HasFlag(ProvisioningStepFlags.DoNotStartAutomaticallyNextStepOnCompletion);
                }

                // We never automatically start the next step in this context, because we aren't returning
                // the next step data to UET; instead we're returning the IPXE script used for booting.
                await _rkmConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                    node.Status.AttestationIdentityKeyFingerprint!,
                    node.Status,
                    cancellationToken);
            }

            return selectedScript ?? defaultScript;
        }

        private class HttpContextProvisioningStepServerContext : IProvisioningStepServerContext
        {
            private readonly HttpContext _httpContext;

            public HttpContextProvisioningStepServerContext(HttpContext httpContext)
            {
                _httpContext = httpContext;
            }

            public IPAddress RemoteIpAddress => _httpContext.Connection.RemoteIpAddress;
        }

        async Task IKestrelRequestHandler.HandleRequestAsync(HttpContext httpContext)
        {
            if (httpContext.Request.Path == "/ipxe.efi")
            {
                _logger.LogInformation($"Transferring ipxe.efi...");
                httpContext.Response.StatusCode = 200;
                httpContext.Response.Headers.Add("Content-Type", "application/octet-stream");
                using (var stream = new FileStream(@"ipxe.efi", FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await stream.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);
                }
                return;
            }
            else if (httpContext.Request.Path == "/autoexec.ipxe")
            {
                _logger.LogInformation($"Transferring autoexec.ipxe...");
                httpContext.Response.StatusCode = 200;
                httpContext.Response.Headers.Add("Content-Type", "text/plain");
                using (var writer = new StreamWriter(httpContext.Response.Body))
                {
                    await writer.WriteAsync(await GetAutoexecScript(httpContext.Connection.RemoteIpAddress, httpContext.RequestAborted));
                }
                return;
            }
            else if (httpContext.Request.Path.StartsWithSegments("/api/node-provisioning", out var remaining))
            {
                if (remaining == "/negotiate-certificate")
                {
                    await _tpmSecuredHttpServer!.HandleNegotiationRequestAsync(httpContext);
                    return;
                }

                var pem = await _tpmSecuredHttpServer!.GetAikPemVerifiedByClientCertificateAsync(httpContext);
                var fingerprint = RkmNodeFingerprint.CreateFromPem(pem);

                if (remaining == "/authorize")
                {
                    var request = await JsonSerializer.DeserializeAsync(
                        httpContext.Request.Body,
                        ApiJsonSerializerContext.WithStringEnum.AuthorizeNodeRequest,
                        httpContext.RequestAborted)!;

                    // @todo: We need to create or update the existing RkmNode object with
                    // the parameters instead of calling Get here.

                    var candidateNode = await _rkmConfigurationSource!.GetRkmNodeByAttestationIdentityKeyPemAsync(
                        pem,
                        httpContext.RequestAborted);
                    if (!(candidateNode?.Spec?.Authorized ?? false) ||
                        string.IsNullOrWhiteSpace(candidateNode?.Spec?.NodeName))
                    {
                        // Not yet authorized.
                        httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        return;
                    }

                    httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
                    await JsonSerializer.SerializeAsync(
                        httpContext.Response.Body,
                        new AuthorizeNodeResponse
                        {
                            NodeName = candidateNode.Spec.NodeName
                        },
                        ApiJsonSerializerContext.WithStringEnum.AuthorizeNodeResponse,
                        httpContext.RequestAborted);
                    return;
                }

                var node = await _rkmConfigurationSource!.GetRkmNodeByAttestationIdentityKeyPemAsync(
                    pem,
                    httpContext.RequestAborted);
                if (!(node?.Spec?.Authorized ?? false) ||
                    string.IsNullOrWhiteSpace(node?.Spec?.NodeName))
                {
                    // Not yet authorized.
                    httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }

                if (remaining == "/step" || remaining == "/step-complete")
                {
                    if (string.IsNullOrWhiteSpace(node?.Status?.Provisioner?.Name))
                    {
                        // No content; the node isn't provisioning or has run out of steps.
                        httpContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                        return;
                    }

                    var serverContext = new HttpContextProvisioningStepServerContext(httpContext);

                    var provisioner = await _rkmConfigurationSource.GetRkmNodeProvisionerAsync(
                        node.Status.Provisioner.Name,
                        _jsonSerializerContext.RkmNodeProvisionerSpec,
                        httpContext.RequestAborted);
                    if (provisioner == null ||
                        (provisioner.GetHash(_jsonSerializerContext.RkmNodeProvisioner) != node.Status.Provisioner.Hash && !string.IsNullOrWhiteSpace(node.Status.Provisioner.Hash)) ||
                        (provisioner.Spec?.Steps?.Count ?? 0) <= (node.Status.Provisioner.CurrentStepIndex ?? 0))
                    {
                        // Provisioner changed or was invalidated since this client started provisioning.
                        httpContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                        return;
                    }

                    var currentStep = provisioner.Spec!.Steps![node.Status.Provisioner.CurrentStepIndex!.Value];
                    var provisioningStep = _provisioningSteps.First(x => string.Equals(x.Type, currentStep?.Type, StringComparison.OrdinalIgnoreCase));

                    if (remaining == "/step")
                    {
                        if (!(node.Status.Provisioner.CurrentStepStarted ?? false))
                        {
                            await provisioningStep.ExecuteOnServerUncastedBeforeAsync(
                                currentStep!.DynamicSettings,
                                node.Status,
                                serverContext,
                                httpContext.RequestAborted);

                            node.Status.Provisioner.CurrentStepStarted = true;

                            await _rkmConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                                fingerprint,
                                node.Status,
                                httpContext.RequestAborted);
                        }

                        // Serialize the current step to the client.
                        // @todo: Replace variables in step config...
                        var currentStepSerialized = JsonSerializer.Serialize(currentStep, _jsonSerializerContext.RkmNodeProvisionerStep);
                        httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
                        httpContext.Response.Headers.Add("Content-Type", "application/json");
                        using (var writer = new StreamWriter(httpContext.Response.Body))
                        {
                            await writer.WriteAsync(currentStepSerialized);
                        }
                        return;
                    }
                    else if (remaining == "/step-complete")
                    {
                        if (!(node.Status.Provisioner.CurrentStepStarted ?? false))
                        {
                            // The /step endpoint must be called first because this step hasn't started.
                            _logger.LogInformation($"Step {node.Status.Provisioner.CurrentStepIndex} can't be completed, because it hasn't been started yet.");
                            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            return;
                        }

                        await provisioningStep.ExecuteOnServerUncastedAfterAsync(
                            currentStep!.DynamicSettings,
                            node.Status,
                            serverContext,
                            httpContext.RequestAborted);

                        // Increment current step index and then update node status.
                        if (!node.Status.Provisioner.CurrentStepIndex.HasValue)
                        {
                            node.Status.Provisioner.CurrentStepIndex = 0;
                        }
                        node.Status.Provisioner.CurrentStepIndex += 1;
                        if (node.Status.Provisioner.CurrentStepIndex >= provisioner.Spec.Steps.Count)
                        {
                            node.Status.Provisioner = null;
                        }
                        else
                        {
                            node.Status.Provisioner.CurrentStepStarted = !provisioningStep.Flags.HasFlag(ProvisioningStepFlags.DoNotStartAutomaticallyNextStepOnCompletion);
                        }

                        // If we completed the last step, return 204. Otherwise, serialize the 
                        // next step as if /step had been called.
                        if (node.Status.Provisioner == null || provisioningStep.Flags.HasFlag(ProvisioningStepFlags.DoNotStartAutomaticallyNextStepOnCompletion))
                        {
                            await _rkmConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                                fingerprint,
                                node.Status,
                                httpContext.RequestAborted);

                            httpContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                        }
                        else
                        {
                            var nextStep = provisioner.Spec!.Steps[node.Status.Provisioner!.CurrentStepIndex!.Value];

                            var nextProvisioningStep = _provisioningSteps.First(x => string.Equals(x.Type, nextStep?.Type, StringComparison.OrdinalIgnoreCase));
                            await nextProvisioningStep.ExecuteOnServerUncastedBeforeAsync(
                                nextStep!.DynamicSettings,
                                node.Status,
                                serverContext,
                                httpContext.RequestAborted);

                            await _rkmConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                                fingerprint,
                                node.Status,
                                httpContext.RequestAborted);

                            var nextStepSerialized = JsonSerializer.Serialize(nextStep, _jsonSerializerContext.RkmNodeProvisionerStep);
                            httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
                            httpContext.Response.Headers.Add("Content-Type", "application/json");
                            using (var writer = new StreamWriter(httpContext.Response.Body))
                            {
                                await writer.WriteAsync(nextStepSerialized);
                            }
                        }
                        return;
                    }
                }
            }

            httpContext.Response.StatusCode = 404;
            return;
        }
    }
}
