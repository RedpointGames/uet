namespace Redpoint.GrpcPipes
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Connections;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Logging;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Net;

    internal sealed class AspNetGrpcPipeServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T> : IGrpcPipeServer<T>, IAsyncDisposable where T : class
    {
        private readonly string _pipePath;
        private readonly T _instance;
        private readonly GrpcPipeNamespace _pipeNamespace;
        private readonly ILogger<AspNetGrpcPipeServer<T>> _logger;
        private WebApplication? _app;
        private FileStream? _pipePointerStream;
        private bool _isNetwork;
        private int _networkPort;

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
            _app = null;
            _isNetwork = false;
            _networkPort = 0;
        }

        public AspNetGrpcPipeServer(
            ILogger<AspNetGrpcPipeServer<T>> logger,
            T instance)
        {
            _logger = logger;
            _pipePath = string.Empty;
            _instance = instance;
            _pipeNamespace = GrpcPipeNamespace.User;
            _app = null;
            _isNetwork = true;
            _networkPort = 0;
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
                    GrpcPipeLog.GrpcServerStarting(_logger);

                    if (!_isNetwork)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_pipePath)!);
                    }

                    var builder = WebApplication.CreateBuilder();
                    builder.Logging.ClearProviders();
#pragma warning disable CA2000 // Dispose objects before losing scope
                    builder.Logging.AddProvider(new ForwardingLoggerProvider(_logger));
#pragma warning restore CA2000 // Dispose objects before losing scope
                    builder.Services.AddGrpc(options =>
                    {
                        // Allow unlimited message sizes.
                        options.MaxReceiveMessageSize = null;
                        options.MaxSendMessageSize = null;
                    });
                    builder.Services.Add(new ServiceDescriptor(
                        typeof(T),
                        _instance));
                    builder.WebHost.ConfigureKestrel(serverOptions =>
                    {
                        if (_isNetwork)
                        {
                            serverOptions.Listen(
                                new IPEndPoint(IPAddress.Any, 0),
                                listenOptions =>
                                {
                                    listenOptions.Protocols = HttpProtocols.Http2;
                                });
                        }
                        else
                        {
                            if (OperatingSystem.IsWindows())
                            {
                                // Pick a free TCP port and listen on that. Unix sockets are broken
                                // on Windows (see https://github.com/dotnet/aspnetcore/issues/47043#issuecomment-1589922597),
                                // so until we can move to .NET 8 with named pipes, we have to do this
                                // jank workaround.
                                GrpcPipeLog.TcpSocketFallback(_logger);
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
                        }
                    });

                    app = builder.Build();
                    app.UseRouting();
                    app.MapGrpcService<T>();

                    await app.StartAsync().ConfigureAwait(false);

                    if (_isNetwork)
                    {
                        _networkPort = new Uri(app.Urls.First()).Port;
                    }
                    else
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            var pointerContent = $"{AspNetGrpcPipeFactory._httpPointerPrefix}{app.Urls.First()}";
                            GrpcPipeLog.WrotePointerFile(_logger, pointerContent, _pipePath);
                            _pipePointerStream = new FileStream(
                                _pipePath,
                                FileMode.Create,
                                FileAccess.ReadWrite,
                                FileShare.Read | FileShare.Delete,
                                4096,
                                FileOptions.DeleteOnClose);
                            using (var writer = new StreamWriter(_pipePointerStream, leaveOpen: true))
                            {
                                await writer.WriteAsync(pointerContent).ConfigureAwait(false);
                                await writer.FlushAsync().ConfigureAwait(false);
                            }
                            await _pipePointerStream.FlushAsync().ConfigureAwait(false);
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
                    }

                    GrpcPipeLog.GrpcServerStarted(_logger);
                    _app = app;
                    return;
                }
                catch (IOException ex) when ((
                    // Unix socket is in use by another process.
                    ex.InnerException is AddressInUseException ||
                    // Pointer file is open by another server.
                    ex.Message.Contains("used by another process", StringComparison.OrdinalIgnoreCase) ||
                    // Old Unix socket on Windows which we can't write the pointer file into (because it's still a Unix socket).
                    ex.Message.Contains("cannot be accessed by the system", StringComparison.OrdinalIgnoreCase)) && File.Exists(_pipePath))
                {
                    if (!_isNetwork)
                    {
                        // Remove the existing pipe. Newer servers always take over from older ones.
                        if (OperatingSystem.IsWindows())
                        {
                            if (_pipePointerStream != null)
                            {
                                await _pipePointerStream.DisposeAsync().ConfigureAwait(false);
                                _pipePointerStream = null;
                            }
                            GrpcPipeLog.RemovingPointerFile(_logger, _pipePath);
                        }
                        else
                        {
                            GrpcPipeLog.RemovingUnixSocket(_logger, _pipePath);
                        }
                    }
                    if (app != null)
                    {
                        await app.StopAsync().ConfigureAwait(false);
                        app = null;
                    }
                    if (!_isNetwork)
                    {
                        File.Delete(_pipePath);
                    }
                    continue;
                }
            } while (true);
        }

        public async Task StopAsync()
        {
            if (!_isNetwork)
            {
                if (_pipePointerStream != null)
                {
                    await _pipePointerStream.DisposeAsync().ConfigureAwait(false);
                    _pipePointerStream = null;
                }
            }

            if (_app != null)
            {
                await _app.StopAsync().ConfigureAwait(false);
                await _app.DisposeAsync().ConfigureAwait(false);
                _app = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
        }

        public int NetworkPort
        {
            get
            {
                if (_isNetwork)
                {
                    return _networkPort;
                }

                throw new NotSupportedException();
            }
        }
    }
}