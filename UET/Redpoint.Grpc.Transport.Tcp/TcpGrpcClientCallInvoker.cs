namespace Redpoint.Grpc.Transport.Tcp
{
    using global::Grpc.Core;
    using System.Net;

    public sealed class TcpGrpcClientCallInvoker : CallInvoker
    {
        private readonly IPEndPoint _endpoint;

        public TcpGrpcClientCallInvoker(IPEndPoint endpoint)
        {
            _endpoint = endpoint;
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
            var call = new TcpGrpcAsyncUnaryCall<TRequest, TResponse>(_endpoint, method, options, request);
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