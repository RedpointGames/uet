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
    using System.Net;

    internal class AspNetGrpcPipeServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T> : IGrpcPipeServer<T> where T : class
    {
        private readonly string _pipePath;
        private readonly T _instance;
        private readonly GrpcPipeNamespace _pipeNamespace;
        private readonly ILogger<AspNetGrpcPipeServer<T>> _logger;
        private WebApplication? _app;
        private FileStream? _pipePointerStream;

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
            string pipePath,
            T instance,
            GrpcPipeNamespace pipeNamespace)
        {
            _logger = logger;
            _pipePath = pipePath;
            _instance = instance;
            _pipeNamespace = pipeNamespace;
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

                    Directory.CreateDirectory(Path.GetDirectoryName(_pipePath)!);
                    var builder = WebApplication.CreateBuilder();
                    builder.Logging.ClearProviders();
                    builder.Logging.AddProvider(new ForwardingLoggerProvider(_logger));
                    builder.Services.AddGrpc();
                    builder.Services.Add(new ServiceDescriptor(
                        typeof(T),
                        _instance));
                    builder.WebHost.ConfigureKestrel(serverOptions =>
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            // Pick a free TCP port and listen on that. Unix sockets are broken
                            // on Windows (see https://github.com/dotnet/aspnetcore/issues/47043#issuecomment-1589922597),
                            // so until we can move to .NET 8 with named pipes, we have to do this
                            // jank workaround.
                            _logger.LogTrace("Using TCP socket with plain text pointer file to workaround issue in .NET 7 where Unix sockets do not work on Windows.");
                            serverOptions.Listen(
                                new IPEndPoint(IPAddress.Loopback, 0),
                                listenOptions =>
                                {
                                    listenOptions.Protocols = HttpProtocols.Http2;
                                });
                        }
                        else
                        {
                            serverOptions.ListenUnixSocket(
                                _pipePath,
                                listenOptions =>
                                {
                                    listenOptions.Protocols = HttpProtocols.Http2;
                                });
                        }
                    });

                    app = builder.Build();
                    app.UseRouting();
                    app.MapGrpcService<T>();

                    await app.StartAsync();

                    if (OperatingSystem.IsWindows())
                    {
                        var pointerContent = $"pointer: {app.Urls.First()}";
                        _logger.LogTrace($"Wrote pointer file with content '{pointerContent}' to: {_pipePath}");
                        _pipePointerStream = new FileStream(
                            _pipePath,
                            FileMode.Create,
                            FileAccess.ReadWrite,
                            FileShare.Read | FileShare.Delete,
                            4096,
                            FileOptions.DeleteOnClose);
                        using (var writer = new StreamWriter(_pipePointerStream, leaveOpen: true))
                        {
                            writer.Write(pointerContent);
                            writer.Flush();
                        }
                        _pipePointerStream.Flush();
                        // @note: Now we hold the FileStream open until we shutdown and then let FileOptions.DeleteOnClose delete it.
                    }
                    else if (_pipeNamespace == GrpcPipeNamespace.Computer)
                    {
                        // Allow everyone to access the pipe if it's meant to be available system-wide.
                        File.SetUnixFileMode(
                            _pipePath,
                            UnixFileMode.UserRead |
                            UnixFileMode.UserWrite |
                            UnixFileMode.UserExecute |
                            UnixFileMode.GroupRead |
                            UnixFileMode.GroupWrite |
                            UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead |
                            UnixFileMode.OtherWrite |
                            UnixFileMode.OtherExecute);
                    }

                    _logger.LogTrace("gRPC server started successfully.");
                    _app = app;
                    return;
                }
                catch (IOException ex) when ((
                    // Unix socket is in use by another process.
                    ex.InnerException is AddressInUseException ||
                    // Pointer file is open by another server.
                    ex.Message.Contains("used by another process") ||
                    // Old Unix socket on Windows which we can't write the pointer file into (because it's still a Unix socket).
                    ex.Message.Contains("cannot be accessed by the system")) && File.Exists(_pipePath))
                {
                    // Remove the existing pipe. Newer servers always take over from older ones.
                    if (OperatingSystem.IsWindows())
                    {
                        if (_pipePointerStream != null)
                        {
                            _pipePointerStream.Dispose();
                            _pipePointerStream = null;
                        }
                        _logger.LogTrace($"Removing existing pointer file from: {_pipePath}");
                    }
                    else
                    {
                        _logger.LogTrace($"Removing existing UNIX socket from: {_pipePath}");
                    }
                    if (app != null)
                    {
                        await app.StopAsync();
                        app = null;
                    }
                    File.Delete(_pipePath);
                    continue;
                }
            } while (true);
        }

        public async Task StopAsync()
        {
            if (_pipePointerStream != null)
            {
                _pipePointerStream.Dispose();
                _pipePointerStream = null;
            }

            if (_app != null)
            {
                await _app.StopAsync();
                _app = null;
            }
        }
    }
}