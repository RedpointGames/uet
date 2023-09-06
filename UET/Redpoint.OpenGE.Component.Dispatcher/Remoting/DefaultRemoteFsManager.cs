namespace Redpoint.OpenGE.Component.Dispatcher.Remoting
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.Logging;
    using Redpoint.Rfs.WinFsp;
    using System;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    internal class DefaultRemoteFsManager : IRemoteFsManager, IAsyncDisposable
    {
        private readonly MutexSlim _startLock;
        private readonly ILogger<DefaultRemoteFsManager> _logger;
        private int? _listeningPort;
        private WebApplication? _app;

        public DefaultRemoteFsManager(
            ILogger<DefaultRemoteFsManager> logger)
        {
            _startLock = new MutexSlim();
            _logger = logger;
        }

        public async Task<int> StartRemoteFsIfNeededAsync()
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                throw new PlatformNotSupportedException("RemoteFs is not supported on non-Windows platforms.");
            }

            if (_listeningPort != null)
            {
                return _listeningPort.Value;
            }

            using (await _startLock.WaitAsync())
            {
                var builder = WebApplication.CreateBuilder();
                builder.Logging.ClearProviders();
                builder.Logging.AddProvider(new ForwardingLoggerProvider(_logger));
                builder.Services.AddGrpc(options =>
                {
                    // Allow unlimited message sizes.
                    options.MaxReceiveMessageSize = null;
                    options.MaxSendMessageSize = null;
                });
                builder.Services.Add(new ServiceDescriptor(
                    typeof(WindowsRfs.WindowsRfsBase),
                    new WindowsRfsHost(_logger)));
                builder.WebHost.ConfigureKestrel(serverOptions =>
                {
                    serverOptions.Listen(
                        new IPEndPoint(IPAddress.Any, 0),
                        listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http2;
                        });
                });

                var app = builder.Build();
                app.UseRouting();
                app.MapGrpcService<WindowsRfs.WindowsRfsBase>();

                await app.StartAsync();

                _app = app;
                _listeningPort = new Uri(app.Urls.First()).Port;
                return _listeningPort.Value;
            }
        }

        public async ValueTask DisposeAsync()
        {
            using (await _startLock.WaitAsync())
            {
                if (_app != null)
                {
                    await _app.StopAsync();
                }
                _app = null;
                _listeningPort = null;
            }
        }
    }
}
