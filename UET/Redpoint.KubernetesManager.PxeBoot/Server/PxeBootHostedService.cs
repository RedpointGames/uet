namespace Redpoint.KubernetesManager.PxeBoot.Server
{
    using GitHub.JPMikkers.Dhcp;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.Kestrel;
    using System;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using Tftp.Net;

    internal class PxeBootHostedService : IHostedService, IDisposable, IDhcpMessageInterceptor, IKestrelRequestHandler
    {
        private readonly ILogger<PxeBootHostedService> _logger;
        private readonly IDhcpServerFactory _dhcpServerFactory;
        private readonly PxeBootServerOptions _options;
        private readonly ICommandInvocationContext _commandInvocationContext;
        private readonly IKestrelFactory _kestrelFactory;

        private TftpServer? _tftpServer;
        private IDhcpServer? _dhcpServer;
        private KestrelServer? _kestrelServer;

        public PxeBootHostedService(
            ILogger<PxeBootHostedService> logger,
            IDhcpServerFactory dhcpServerFactory,
            PxeBootServerOptions options,
            ICommandInvocationContext commandInvocationContext,
            IKestrelFactory kestrelFactory)
        {
            _logger = logger;
            _dhcpServerFactory = dhcpServerFactory;
            _options = options;
            _commandInvocationContext = commandInvocationContext;
            _kestrelFactory = kestrelFactory;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
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

            var kestrelOptions = new KestrelServerOptions();
            kestrelOptions.ListenAnyIP(8790);

            // @todo: Listen on HTTPS and validate client certificates issued by RKM.
            // RKM should check TPM attestation and issue a certificate for further communication.

            /*
            kestrelOptions.ListenAnyIP(8791, options =>
            {
                options.UseHttps(options =>
                {
                    options.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                    options.ClientCertificateValidation
                });
            });
            */

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
                        writer.Write(GetAutoexecScript(ipEndpoint.Address));
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

        private static string GetAutoexecScript(IPAddress sourceIpAddress)
        {
            return
                """
                #!ipxe
                dhcp
                shell
                """;
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
                    await writer.WriteAsync(GetAutoexecScript(httpContext.Connection.RemoteIpAddress));
                }
                return;
            }

            httpContext.Response.StatusCode = 404;
            return;

        }
    }
}
