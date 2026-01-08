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
    using Redpoint.Hashing;
    using Redpoint.Kestrel;
    using Redpoint.KubernetesManager.Configuration.Json;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Api;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.NodeProvisioning;
    using Redpoint.KubernetesManager.PxeBoot.Server.Handlers;
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
    using System.Threading;
    using System.Threading.Tasks;
    using Tftp.Net;

    internal class PxeBootHostedService : IHostedService, IAsyncDisposable, IDhcpMessageInterceptor, IKestrelRequestHandler
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
        private readonly IPxeBootTftpRequestHandler _pxeBootTftpRequestHandler;
        private readonly IPxeBootHttpRequestHandler _pxeBootHttpRequestHandler;
        private readonly PxeBootServerContext _serverContext;
        private TftpServer? _tftpServer;
        private IDhcpServer? _dhcpServer;
        private KestrelServer? _kestrelServer;
        private ITpmSecuredHttpServer? _tpmSecuredHttpServer;

        private const int _httpPort = 8790;
        private const int _httpsPort = 8791;

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
            IServiceProvider serviceProvider,
            IPxeBootTftpRequestHandler pxeBootTftpRequestHandler,
            IPxeBootHttpRequestHandler pxeBootHttpRequestHandler)
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
            _pxeBootTftpRequestHandler = pxeBootTftpRequestHandler;
            _pxeBootHttpRequestHandler = pxeBootHttpRequestHandler;

            _serverContext = new PxeBootServerContext
            {
                ConfigurationSource = _commandInvocationContext.ParseResult.GetValueForOption(_options.Source) switch
                {
                    PxeBootServerSource.Test => new TestRkmConfigurationSource(
                        _loggerFactory.CreateLogger<TestRkmConfigurationSource>()),
                    PxeBootServerSource.KubernetesDefault => new KubernetesRkmConfigurationSource(
                        new KubernetesWithDeserializeFix(KubernetesClientConfiguration.BuildDefaultConfig())),
                    PxeBootServerSource.KubernetesInCluster => new KubernetesRkmConfigurationSource(
                        new KubernetesWithDeserializeFix(KubernetesClientConfiguration.InClusterConfig())),
                    _ => throw new InvalidOperationException("Invalid configuration source selected."),
                },
                StaticFilesDirectory = _commandInvocationContext.ParseResult.GetValueForOption(_options.StaticFiles)!,
                StorageFilesDirectory = _commandInvocationContext.ParseResult.GetValueForOption(_options.StorageFiles)!,
                HostAddress = _commandInvocationContext.ParseResult.GetValueForOption(_options.HostAddress) ?? IPAddress.Parse("192.168.0.1"),
                HostHttpPort = _httpPort,
                HostHttpsPort = _httpsPort,
                JsonSerializerContext = KubernetesRkmJsonSerializerContext.CreateStringEnumWithAdditionalConverters(
                new RkmNodeProvisionerStepJsonConverter(provisioningSteps)),
                GetTpmSecuredHttpServer = () => _tpmSecuredHttpServer!,
            };
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_tftpServer != null)
            {
                await _tftpServer.DisposeAsync();
            }

            _tftpServer = new TftpServer(IPAddress.Any);
            _tftpServer.OnReadRequest.Add(OnTftpReadRequest);
            _tftpServer.OnWriteRequest.Add(OnTftpWriteRequest);
            _tftpServer.OnError.Add(OnTftpError);
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
            if (_tftpServer != null)
            {
                await _tftpServer.DisposeAsync();
            }
            _tftpServer = null;
        }

        public async ValueTask DisposeAsync()
        {
            if (_tftpServer != null)
            {
                await _tftpServer.DisposeAsync();
            }
            _tftpServer = null;

            _dhcpServer?.Dispose();
            _dhcpServer = null;

            _kestrelServer?.Dispose();
            _kestrelServer = null;
        }

        #region TFTP Request Handling

        private Task OnTftpError(TftpTransferError error, CancellationToken cancellationToken)
        {
            _logger.LogError($"TFTP error: {error}");
            return Task.CompletedTask;
        }

        private Task OnTftpWriteRequest(TftpServerEventHandlerArgs args, CancellationToken cancellationToken)
        {
            args.Transfer.Cancel(TftpErrorPacket.AccessViolation);
            return Task.CompletedTask;
        }

        private Task OnTftpReadRequest(TftpServerEventHandlerArgs args, CancellationToken cancellationToken)
        {
            return OnTftpReadRequestAsync(args.Transfer, args.EndPoint);
        }

        private async Task OnTftpReadRequestAsync(ITftpTransfer transfer, EndPoint client)
        {
            await _pxeBootTftpRequestHandler.HandleRequestAsync(
                _serverContext,
                transfer,
                client);
        }

        #endregion

        #region DHCP Handling

        void IDhcpMessageInterceptor.Apply(DhcpMessage sourceMsg, DhcpMessage targetMsg)
        {
            var vendorClientMatch = sourceMsg.FindOption<DhcpOptionVendorClassIdentifier>();
            if (vendorClientMatch != null)
            {
                var vendorClient = Encoding.ASCII.GetString(vendorClientMatch?.Data ?? []).Trim();
                if (vendorClient.StartsWith("HTTPClient", StringComparison.Ordinal))
                {
                    _logger.LogInformation("DHCP boot from HTTPClient...");
                    targetMsg.Options.Add(new DhcpOptionBootFileName($"http://192.168.0.1:{_httpPort}/static/ipxe.efi"));
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

        #endregion

        #region HTTP Handling

        async Task IKestrelRequestHandler.HandleRequestAsync(HttpContext httpContext)
        {
            await _pxeBootHttpRequestHandler.HandleRequestAsync(
                _serverContext,
                httpContext);
        }

        #endregion
    }
}
