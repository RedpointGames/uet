using k8s.Models;

namespace Redpoint.KubernetesManager.PxeBoot.Server
{
    using GitHub.JPMikkers.Dhcp;
    using k8s;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
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
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.NetworkInformation;
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

        private const int _httpPort = 8790;
        private const int _httpsPort = 8791;

        private static readonly string[] _staticFileAllowlist = new[]
        {
            "ipxe.efi",
            "bzImage",
            "bzImage.efi",
            "wimboot",
            "background.png",
            "vmlinuz",
            "vmlinuz.efi",
            "rootfs.cpio",
            "initrd",
        };

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
                    _dhcpServer.LeaseTime = Utils.InfiniteTimeSpan;
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
            kestrelOptions.ListenAnyIP(_httpPort);
            kestrelOptions.ListenAnyIP(_httpsPort, options =>
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

        private IPAddress GetHostAddress()
        {
            return _commandInvocationContext.ParseResult.GetValueForOption(_options.HostAddress) ?? IPAddress.Parse("192.168.0.1");
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

                if (_staticFileAllowlist.Contains(transfer.Filename.TrimStart('/'), StringComparer.Ordinal))
                {
                    var staticFilesPath = _commandInvocationContext.ParseResult.GetValueForOption(_options.StaticFiles)!.FullName;
                    var staticFilePath = Path.Combine(staticFilesPath, transfer.Filename.TrimStart('/'));

                    var fileStream = new FileStream(staticFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    _logger.LogInformation($"Transferring {transfer.Filename.TrimStart('/')} ({fileStream.Length} bytes)...");
                    transfer.Start(fileStream);
                }
                else if (transfer.Filename.TrimStart('/') == "autoexec.ipxe")
                {
                    _logger.LogInformation($"Transferring autoexec.ipxe on TFTP to chainload...");
                    var stream = new MemoryStream();
                    var hostAddress = GetHostAddress().ToString();
                    using (var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true))
                    {
                        writer.Write(
                            $"""
                            #!ipxe
                            dhcp
                            chain --replace http://{hostAddress}:{_httpPort}/autoexec-nodhcp.ipxe
                            """);
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
                    targetMsg.Options.Add(new DhcpOptionBootFileName($"http://192.168.0.1:{_httpPort}/ipxe.efi"));
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

        private async Task<string> GetAutoexecScript(IPAddress sourceIpAddress, bool skipDhcp, CancellationToken cancellationToken)
        {
            var defaultScript =
                $$$"""
                #!ipxe
                {{dhcp}}
                kernel static/vmlinuz quiet nologo=0 rkm-api-address={{provisioner-api-address}} rkm-booted-from-step-index={{booted-from-step-index}}
                initrd static/initrd
                boot
                """;

            var node = await _rkmConfigurationSource!.GetRkmNodeByRegisteredIpAddressAsync(
                sourceIpAddress.ToString(),
                cancellationToken);

            async Task<string> GetSelectedScript()
            {
                if (node == null)
                {
                    return defaultScript;
                }
                if (node.Status?.BootToDisk ?? false)
                {
                    // Causes PXE boot to exit so the machine will fallthrough to the disk, which should be
                    // the second boot entry in the firmware.
                    node.Status.BootToDisk = false;
                    await _rkmConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                        node.Status.AttestationIdentityKeyFingerprint!,
                        node.Status,
                        cancellationToken);
                    return
                        """
                        #!ipxe
                        echo Boot to disk requested.
                        exit
                        """;
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
                    (provisioner.Spec?.Steps?.Count ?? 0) <= (node.Status.Provisioner.RebootStepIndex ?? 0))
                {
                    return defaultScript;
                }

                var serverContext = new IpxeProvisioningStepServerContext(sourceIpAddress);

                var rebootStep = provisioner.Spec!.Steps![node.Status.Provisioner.RebootStepIndex!.Value];
                var provisioningRebootStep = _provisioningSteps.First(x => string.Equals(x.Type, rebootStep?.Type, StringComparison.OrdinalIgnoreCase));

                var overrideScript = await provisioningRebootStep.GetIpxeAutoexecScriptOverrideOnServerUncastedAsync(
                    rebootStep!.DynamicSettings,
                    node.Status,
                    serverContext,
                    cancellationToken);

                if (provisioningRebootStep.Flags.HasFlag(ProvisioningStepFlags.AssumeCompleteWhenIpxeScriptFetched))
                {
                    await provisioningRebootStep.ExecuteOnServerUncastedAfterAsync(
                        rebootStep!.DynamicSettings,
                        node.Status,
                        serverContext,
                        cancellationToken);

                    // Increment current step index and then update node status.
                    if (!node.Status.Provisioner.CurrentStepIndex.HasValue)
                    {
                        node.Status.Provisioner.CurrentStepIndex = 0;
                    }
                    node.Status.Provisioner.CurrentStepIndex += 1;
                    if (node.Status.Provisioner.CurrentStepIndex <= node.Status.Provisioner.RebootStepIndex)
                    {
                        // Make sure when the client grabs the next step, it's always continuing from the reboot point
                        // if a later step hasn't committed.
                        node.Status.Provisioner.CurrentStepIndex = node.Status.Provisioner.RebootStepIndex + 1;
                    }
                    if (node.Status.Provisioner.CurrentStepIndex >= provisioner.Spec.Steps.Count)
                    {
                        node.Status.Provisioner = null;
                    }
                    else
                    {
                        node.Status.Provisioner.CurrentStepStarted = false;
                    }

                    // We never automatically start the next step in this context, because we aren't returning
                    // the next step data to UET; instead we're returning the IPXE script used for booting.
                    await _rkmConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                        node.Status.AttestationIdentityKeyFingerprint!,
                        node.Status,
                        cancellationToken);
                }

                return overrideScript ?? defaultScript;
            }

            var bootedFromStepIndex = (node?.Status?.Provisioner?.RebootStepIndex ?? -1).ToString(CultureInfo.InvariantCulture);
            _logger.LogInformation($"Informing machine that they are booting from step index {bootedFromStepIndex}.");

            var replacements = new Dictionary<string, string>
            {
                { "booted-from-step-index", bootedFromStepIndex },
                { "dhcp", !skipDhcp ? "dhcp" : string.Empty },
                { "provisioner-api-address", GetHostAddress().ToString() },
            };
            var selectedScript = await GetSelectedScript();
            foreach (var kv in replacements)
            {
                selectedScript = selectedScript.Replace("{{" + kv.Key + "}}", kv.Value, StringComparison.Ordinal);
            }
            return selectedScript;
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
            _logger.LogInformation($"HTTP request to: {httpContext.Request.Path}");

            if (
                httpContext.Request.Path == "/autoexec.ipxe" ||
                httpContext.Request.Path == "/autoexec-nodhcp.ipxe")
            {
                httpContext.Response.StatusCode = 200;
                httpContext.Response.Headers.Add("Content-Type", "text/plain");
                using (var writer = new StreamWriter(httpContext.Response.Body))
                {
                    await writer.WriteAsync(await GetAutoexecScript(
                        httpContext.Connection.RemoteIpAddress,
                        httpContext.Request.Path == "/autoexec-nodhcp.ipxe",
                        httpContext.RequestAborted));
                    await writer.FlushAsync();
                }
                return;
            }
            else if (httpContext.Request.Path.StartsWithSegments("/static", out var staticRemaining))
            {
                var targetFilename = staticRemaining.ToString().TrimStart('/');
                if (targetFilename.Contains('/', StringComparison.Ordinal))
                {
                    httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
                else if (_staticFileAllowlist.Contains(targetFilename, StringComparer.Ordinal))
                {
                    var staticFilesPath = _commandInvocationContext.ParseResult.GetValueForOption(_options.StaticFiles)!.FullName;
                    var staticFilePath = Path.Combine(staticFilesPath, targetFilename);

                    httpContext.Response.StatusCode = 200;
                    httpContext.Response.Headers.Add(
                        "Content-Type",
                        targetFilename.EndsWith(".ipxe", StringComparison.Ordinal)
                            ? "text/plain"
                            : "application/octet-stream");
                    using (var stream = new FileStream(staticFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        _logger.LogInformation($"Transferring {staticFilePath} ({stream.Length} bytes)...");
                        await stream.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);
                    }
                }
                else
                {
                    httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
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
                    var request = (await JsonSerializer.DeserializeAsync(
                        httpContext.Request.Body,
                        ApiJsonSerializerContext.WithStringEnum.AuthorizeNodeRequest,
                        httpContext.RequestAborted))!;

                    var candidateNode = await _rkmConfigurationSource!.CreateOrUpdateRkmNodeByAttestationIdentityKeyPemAsync(
                        pem,
                        [RkmNodeRole.Worker],
                        false,
                        request.CapablePlatforms,
                        request.Architecture,
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

                if (remaining == "/reboot-to-disk")
                {
                    _logger.LogInformation($"Node {node.Name()} is requesting to boot to disk on next PXE boot.");

                    node.Status ??= new();
                    node.Status.BootToDisk = true;

                    await _rkmConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                        fingerprint,
                        node.Status,
                        httpContext.RequestAborted);

                    httpContext.Response.StatusCode = 200;

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

                    if (remaining == "/step" &&
                        httpContext.Request.Query.TryGetValue("initial", out var initial) && initial.FirstOrDefault() == "true")
                    {
                        if (!httpContext.Request.Query.TryGetValue("bootedFromStepIndex", out var bootedFromStepIndex) ||
                            string.IsNullOrWhiteSpace(bootedFromStepIndex) ||
                            int.Parse(bootedFromStepIndex!, CultureInfo.InvariantCulture) != (node.Status.Provisioner.RebootStepIndex ?? -1))
                        {
                            // The machine didn't boot with the expected autoexec.ipxe script, usually because
                            // the IP address of the machine during PXE boot and the IP address of the machine
                            // during provisioning inside initrd is different. When this happens, autoexec.ipxe isn't
                            // serving the desired script (which could result in the following provisioning steps
                            // running in the complete wrong environment). Therefore this is a permanent failure that
                            // needs to be fixed manually.
                            _logger.LogError($"Machine should have booted from step index {(node.Status.Provisioner.RebootStepIndex ?? -1)}, but booted from {bootedFromStepIndex} instead.");
                            httpContext.Response.StatusCode = (int)HttpStatusCode.FailedDependency;

                            // Rewind the provisioner state to the start so that the machine can recovery by rebooting
                            // and starting the process again from the start.
                            node.Status.Provisioner.LastStepCommittedIndex = null;
                            node.Status.Provisioner.RebootStepIndex = null;
                            node.Status.Provisioner.CurrentStepIndex = 0;
                            node.Status.Provisioner.CurrentStepStarted = false;
                            await _rkmConfigurationSource.UpdateRkmNodeStatusByAttestationIdentityKeyFingerprintAsync(
                                fingerprint,
                                node.Status,
                                httpContext.RequestAborted);

                            return;
                        }

                        // Reset the "currentStepIndex" to the index after "lastStepCommittedIndex". This ensures
                        // that step completion status don't carry over reboots unless explicitly committed.
                        var lastStepCommittedIndex = node.Status.Provisioner.LastStepCommittedIndex ?? -1;
                        var rebootStepIndex = node.Status.Provisioner.RebootStepIndex ?? -1;
                        if (lastStepCommittedIndex < rebootStepIndex)
                        {
                            // Last committed step must always at least be the reboot step index.
                            lastStepCommittedIndex = rebootStepIndex;
                        }
                        _logger.LogInformation($"Setting current step to {lastStepCommittedIndex + 1} during initial step fetch. Last committed step index is {lastStepCommittedIndex}, reboot step index is {rebootStepIndex}.");
                        node.Status.Provisioner.CurrentStepIndex = lastStepCommittedIndex + 1;
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

                            if (provisioningStep.Flags.HasFlag(ProvisioningStepFlags.SetAsRebootStepIndex))
                            {
                                // Set the reboot step index if this is a reboot step. This must be done during /step, since
                                // the reboot steps don't "complete" like other steps.
                                _logger.LogInformation($"Setting reboot step index to {node.Status.Provisioner.CurrentStepIndex}.");
                                node.Status.Provisioner.RebootStepIndex = node.Status.Provisioner.CurrentStepIndex;
                            }

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
                        if (provisioningStep.Flags.HasFlag(ProvisioningStepFlags.CommitOnCompletion))
                        {
                            node.Status.Provisioner.LastStepCommittedIndex = node.Status.Provisioner.CurrentStepIndex;
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

                            if (nextProvisioningStep.Flags.HasFlag(ProvisioningStepFlags.SetAsRebootStepIndex))
                            {
                                // Set the reboot step index if this is a reboot step. This must be done when a reboot step is
                                // started as part of /step-complete's next handling, since the reboot steps don't "complete" like other steps.
                                _logger.LogInformation($"Setting reboot step index to {node.Status.Provisioner.CurrentStepIndex}.");
                                node.Status.Provisioner.RebootStepIndex = node.Status.Provisioner.CurrentStepIndex;
                            }

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
