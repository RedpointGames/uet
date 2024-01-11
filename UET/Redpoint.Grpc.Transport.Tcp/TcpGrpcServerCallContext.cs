namespace Redpoint.Grpc.Transport.Tcp
{
    using global::Grpc.Core;
    using Redpoint.Concurrency;
    using System;
    using System.Threading.Tasks;

    internal class TcpGrpcServerCallContext : ServerCallContext, IDisposable
    {
        private readonly string _method;
        private readonly string _host;
        private readonly string _peer;
        private readonly DateTime? _deadline;
        private readonly Metadata _requestHeaders;
        private readonly TcpGrpcTransportConnection _connection;
        private readonly Mutex _writeMutex;
        private readonly Metadata _trailers;
        private readonly AuthContext _authContext;
        private bool _headersSent;
        private Status _status;
        private WriteOptions? _writeOptions;

        public TcpGrpcServerCallContext(
            string method,
            string host,
            string peer,
            DateTime? deadline,
            Metadata requestHeaders,
            TcpGrpcTransportConnection connection,
            Mutex writeMutex,
            CancellationToken parentCancellationToken)
        {
            _method = method;
            _host = host;
            _peer = peer;
            _deadline = deadline;
            _requestHeaders = requestHeaders;
            _connection = connection;
            _writeMutex = writeMutex;
            _trailers = new Metadata();
            _authContext = new AuthContext(null, new Dictionary<string, List<AuthProperty>>());
            _headersSent = false;
            _status = Status.DefaultCancelled;
            _writeOptions = null;
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentCancellationToken);
        }

        public CancellationTokenSource CancellationTokenSource { get; }

        protected override string MethodCore => _method;

        protected override string HostCore => _host;

        protected override string PeerCore => _peer;

        protected override DateTime DeadlineCore => _deadline ?? DateTime.MaxValue;

        protected override Metadata RequestHeadersCore => _requestHeaders;

        protected override CancellationToken CancellationTokenCore => CancellationTokenSource.Token;

        protected override Metadata ResponseTrailersCore => _trailers;

        protected override Status StatusCore { get => _status; set => _status = value; }

        protected override WriteOptions? WriteOptionsCore { get => _writeOptions; set => _writeOptions = value; }

        protected override AuthContext AuthContextCore => _authContext;

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        {
            throw new NotSupportedException();
        }

        protected override async Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        {
            if (_headersSent)
            {
                return;
            }

            using (await _writeMutex.WaitAsync(CancellationTokenSource.Token).ConfigureAwait(false))
            {
                if (_headersSent)
                {
                    return;
                }

                await _connection.WriteAsync(new TcpGrpcMessage
                {
                    Type = TcpGrpcMessageType.ResponseHeaders
                }).ConfigureAwait(false);
                await _connection.WriteAsync(TcpGrpcMetadataConverter.Convert(responseHeaders)).ConfigureAwait(false);
                _headersSent = true;
            }
        }

        public void Dispose()
        {
            CancellationTokenSource.Dispose();
        }
    }
}
