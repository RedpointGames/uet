namespace Redpoint.GrpcPipes
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Connections;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics.CodeAnalysis;

    internal class AspNetGrpcPipeServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T> : IGrpcPipeServer<T> where T : class
    {
        private readonly string _unixSocketPath;
        private readonly T _instance;
        private readonly ILogger<AspNetGrpcPipeServer<T>> _logger;
        private WebApplication? _app;

        private class ForwardingLoggerProvider : ILoggerProvider
        {
            private readonly ILogger _logger;

            private class ForwardingLogger : ILogger
            {
                private readonly ILogger _logger;

                public ForwardingLogger(ILogger logger)
                {
                    _logger = logger;
                }

                public IDisposable? BeginScope<TState>(TState state) where TState : notnull
                {
                    return _logger.BeginScope(state);
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return _logger.IsEnabled(logLevel);
                }

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    if (logLevel == LogLevel.Information)
                    {
                        logLevel = LogLevel.Trace;
                    }
                    _logger.Log(logLevel, eventId, state, exception, formatter);
                }
            }

            public ForwardingLoggerProvider(ILogger logger)
            {
                _logger = logger;
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new ForwardingLogger(_logger);
            }

            public void Dispose()
            {
            }
        }

        public AspNetGrpcPipeServer(
            ILogger<AspNetGrpcPipeServer<T>> logger,
            string unixSocketPath,
            T instance)
        {
            _logger = logger;
            _unixSocketPath = unixSocketPath;
            _instance = instance;
        }

        public async Task StartAsync()
        {
            if (_app != null)
            {
                return;
            }
            do
            {
                WebApplication? app = null;
                try
                {
                    _logger.LogTrace("Attempting to start gRPC server...");

                    Directory.CreateDirectory(Path.GetDirectoryName(_unixSocketPath)!);
                    var builder = WebApplication.CreateBuilder();
                    builder.Logging.ClearProviders();
                    builder.Logging.AddProvider(new ForwardingLoggerProvider(_logger));
                    builder.Services.AddGrpc();
                    builder.Services.Add(new ServiceDescriptor(
                        typeof(T),
                        _instance));
                    builder.WebHost.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.ListenUnixSocket(
                            _unixSocketPath,
                            listenOptions =>
                            {
                                listenOptions.Protocols = HttpProtocols.Http2;
                            });
                    });

                    app = builder.Build();
                    app.UseRouting();
                    app.MapGrpcService<T>();

                    await app.StartAsync();

                    _logger.LogTrace("gRPC server started successfully.");
                    _app = app;
                    return;
                }
                catch (IOException ex) when (ex.InnerException is AddressInUseException && File.Exists(_unixSocketPath))
                {
                    // Remove the existing pipe. Newer servers always take over from older ones.
                    _logger.LogTrace($"Removing existing UNIX socket from: {_unixSocketPath}");
                    if (app != null)
                    {
                        await app.StopAsync();
                        app = null;
                    }
                    File.Delete(_unixSocketPath);
                    continue;
                }
            } while (true);
        }

        public async Task StopAsync()
        {
            if (_app != null)
            {
                await _app.StopAsync();
                _app = null;
            }
        }
    }
}