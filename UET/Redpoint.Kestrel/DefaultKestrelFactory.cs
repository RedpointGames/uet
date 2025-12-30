namespace Redpoint.Kestrel
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting.Server;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
    using Microsoft.AspNetCore.WebSockets;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultKestrelFactory : IKestrelFactory, Microsoft.AspNetCore.Hosting.IApplicationLifetime
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public DefaultKestrelFactory(
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        public CancellationToken ApplicationStarted => _hostApplicationLifetime.ApplicationStarted;

        public CancellationToken ApplicationStopping => _hostApplicationLifetime.ApplicationStopping;

        public CancellationToken ApplicationStopped => _hostApplicationLifetime.ApplicationStopped;

        private class KestrelContext
        {
            public required IFeatureCollection Features { get; init; }
        }

        private class KestrelApplication : IHttpApplication<KestrelContext>
        {
            private readonly WebSocketMiddleware _webSocketMiddleware;
            private readonly IKestrelRequestHandler _requestHandler;

            public KestrelApplication(
                IKestrelRequestHandler requestHandler)
            {
                _webSocketMiddleware = new WebSocketMiddleware(ContinueAsync, new OptionsWrapper<WebSocketOptions>(new()));
                _requestHandler = requestHandler;
            }

            private async Task ContinueAsync(HttpContext context)
            {
                await _requestHandler.HandleRequestAsync(context);
            }

            public KestrelContext CreateContext(IFeatureCollection contextFeatures)
            {
                return new KestrelContext
                {
                    Features = contextFeatures
                };
            }

            public void DisposeContext(KestrelContext context, Exception exception)
            {
            }

            public Task ProcessRequestAsync(KestrelContext context)
            {
                return _webSocketMiddleware.Invoke(new DefaultHttpContext(context.Features));
            }
        }

        public async Task<KestrelServer> CreateAndStartServerAsync(
            KestrelServerOptions serverOptions,
            IKestrelRequestHandler requestHandler,
            CancellationToken cancellationToken)
        {
            var transportOptions = new SocketTransportOptions();
            var loggerFactory = new NullLoggerFactory();

            var transportFactory = new SocketTransportFactory(
                new OptionsWrapper<SocketTransportOptions>(transportOptions),
                this,
                loggerFactory);

            var kestrelServer = new KestrelServer(
                new OptionsWrapper<KestrelServerOptions>(serverOptions),
                transportFactory,
                loggerFactory);
            await kestrelServer.StartAsync(
                new KestrelApplication(requestHandler),
                cancellationToken);

            return kestrelServer;
        }

        public void StopApplication()
        {
            _hostApplicationLifetime.StopApplication();
        }
    }
}
