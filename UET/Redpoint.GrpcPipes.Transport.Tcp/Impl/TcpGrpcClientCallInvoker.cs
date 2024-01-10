namespace Redpoint.GrpcPipes.Transport.Tcp.Impl
{
    using global::Grpc.Core;
    using Microsoft.Extensions.Logging;
    using System.Net;

    internal sealed class TcpGrpcClientCallInvoker : CallInvoker
    {
        private readonly IPEndPoint _endpoint;
        private readonly ILogger? _logger;

        public TcpGrpcClientCallInvoker(IPEndPoint endpoint, ILogger? logger = null)
        {
            _endpoint = endpoint;
            _logger = logger;
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request)
        {
            throw new NotSupportedException("Blocking unary calls are not supported by TcpGrpcClientCallInvoker.");
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request)
        {
            var call = new TcpGrpcAsyncUnaryCall<TRequest, TResponse>(_endpoint, _logger, method, options, request);
            return new AsyncUnaryCall<TResponse>(
                call.GetResponseAsync(),
                call.GetResponseHeadersAsync(),
                call.GetStatus,
                call.GetTrailers,
                call.Dispose);
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options)
        {
            throw new NotImplementedException();
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options,
            TRequest request)
        {
            throw new NotImplementedException();
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            string? host,
            CallOptions options)
        {
            throw new NotImplementedException();
        }
    }
}