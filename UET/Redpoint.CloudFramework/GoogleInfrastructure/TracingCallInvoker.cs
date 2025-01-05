namespace Redpoint.CloudFramework.GoogleInfrastructure
{
    using Grpc.Core;
    using Redpoint.CloudFramework.Tracing;

    internal class TracingCallInvoker : CallInvoker
    {
        private readonly CallInvoker _baseCallInvoker;
        private readonly IManagedTracer _managedTracer;

        public TracingCallInvoker(
            CallInvoker baseCallInvoker,
            IManagedTracer managedTracer)
        {
            _baseCallInvoker = baseCallInvoker;
            _managedTracer = managedTracer;
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
        {
            using (_managedTracer.StartSpan("grpc.client_streaming", $"{host}/{method.Name}"))
            {
                return _baseCallInvoker.AsyncClientStreamingCall(method, host, options);
            }
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
        {
            using (_managedTracer.StartSpan("grpc.duplex_streaming", $"{host}/{method.Name}"))
            {
                return _baseCallInvoker.AsyncDuplexStreamingCall(method, host, options);
            }
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            using (_managedTracer.StartSpan("grpc.server_streaming", $"{host}/{method.Name}"))
            {
                return _baseCallInvoker.AsyncServerStreamingCall(method, host, options, request);
            }
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            using (_managedTracer.StartSpan("grpc.call_async", $"{host}/{method.Name}"))
            {
                return _baseCallInvoker.AsyncUnaryCall(method, host, options, request);
            }
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            using (_managedTracer.StartSpan("grpc.call", $"{host}/{method.Name}"))
            {
                return _baseCallInvoker.BlockingUnaryCall(method, host, options, request);
            }
        }
    }
}
