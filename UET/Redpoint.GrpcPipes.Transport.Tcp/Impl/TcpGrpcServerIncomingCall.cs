namespace Redpoint.GrpcPipes.Transport.Tcp.Impl
{
    using Grpc.Core;
    using System;
    using Redpoint.Concurrency;

    internal readonly record struct TcpGrpcServerIncomingCall
    {
        public required TcpGrpcRequest Request { get; init; }
        public required TcpGrpcTransportConnection Connection { get; init; }
        public required string Peer { get; init; }
        public required Action<string> LogTrace { get; init; }

        public TcpGrpcServerCallContext CreateCallContext(
            string methodName,
            CancellationToken cancellationToken)
        {
            return new TcpGrpcServerCallContext(
                methodName,
                string.Empty,
                Peer,
                Request.DeadlineUnixTimeMilliseconds == 0
                    ? null
                    : DateTimeOffset.FromUnixTimeMilliseconds(Request.DeadlineUnixTimeMilliseconds).UtcDateTime,
                Request.HasRequestHeaders
                    ? TcpGrpcMetadataConverter.Convert(Request.RequestHeaders)
                    : new Metadata(),
                Connection,
                new Mutex(),
                cancellationToken);
        }
    }
}
