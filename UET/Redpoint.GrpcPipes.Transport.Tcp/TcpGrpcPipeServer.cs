namespace Redpoint.GrpcPipes.Transport.Tcp
{
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes.Transport.Tcp.Impl;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Net.Sockets;
    using System.Net;
    using System.Threading.Tasks;
    using System.Reflection;
    using Grpc.Core;

    internal sealed class TcpGrpcPipeServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T> : IGrpcPipeServer<T>, IAsyncDisposable
        where T : class
    {
        private readonly string _pipePath;
        private readonly T _instance;
        private readonly GrpcPipeNamespace _pipeNamespace;
        private readonly ILogger<TcpGrpcPipeServer<T>> _logger;
        private FileStream? _pipePointerStream;
        private TcpGrpcServer? _app;
        private bool _isNetwork;
        private bool _isNetworkLoopbackOnly;
        private int _networkPort;

        public TcpGrpcPipeServer(
            ILogger<TcpGrpcPipeServer<T>> logger,
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

        public TcpGrpcPipeServer(
            ILogger<TcpGrpcPipeServer<T>> logger,
            T instance, 
            bool loopbackOnly)
        {
            _logger = logger;
            _pipePath = string.Empty;
            _instance = instance;
            _pipeNamespace = GrpcPipeNamespace.User;
            _app = null;
            _isNetwork = true;
            _isNetworkLoopbackOnly = loopbackOnly;
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
                TcpGrpcServer? app = null;
                try
                {
                    GrpcPipeLog.GrpcServerStarting(_logger);

                    if (!_isNetwork)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_pipePath)!);
                    }

                    var endpoint = new IPEndPoint((_isNetwork && !_isNetworkLoopbackOnly) ? IPAddress.Any : IPAddress.Loopback, 0);
                    var listener = new TcpListener(endpoint);
                    app = new TcpGrpcServer(listener, _logger);
                    endpoint = (IPEndPoint)listener.LocalEndpoint;

                    var binderAttr = typeof(T).GetCustomAttribute<BindServiceMethodAttribute>();
                    if (binderAttr == null)
                    {
                        throw new InvalidOperationException($"{typeof(T).FullName} does not have the grpc::BindServiceMethod attribute.");
                    }
                    var targetMethods = binderAttr.BindType.GetMethods(
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.FlattenHierarchy);
                    var binder = targetMethods
                        .Where(x => x.Name == binderAttr.BindMethodName && x.GetParameters().Length == 2)
                        .First();
                    binder.Invoke(null, BindingFlags.DoNotWrapExceptions, null, [app, _instance], null);

                    if (_isNetwork)
                    {
                        _networkPort = endpoint.Port;
                    }
                    else
                    {
                        var pointerContent = $"{TcpGrpcPipeFactory._tcpPointerPrefix}{endpoint}";
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

                    GrpcPipeLog.GrpcServerStarted(_logger);
                    _app = app;
                    return;
                }
                catch (IOException ex) when ((
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
                        await app.DisposeAsync().ConfigureAwait(false);
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
