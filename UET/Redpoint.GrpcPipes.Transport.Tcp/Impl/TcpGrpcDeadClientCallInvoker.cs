namespace Redpoint.GrpcPipes.Transport.Tcp.Impl
{
    using Grpc.Core;

    internal sealed class TcpGrpcDeadClientCallInvoker : CallInvoker
    {
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "The remote host refused the connection."));
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "The remote host refused the connection."));
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "The remote host refused the connection."));
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "The remote host refused the connection."));
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "The remote host refused the connection."));
        }
    }
}