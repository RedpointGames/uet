namespace Redpoint.GrpcPipes
{
    using Grpc.Core;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides an interface for retrying gRPC requests, even if the gRPC request fails due to DeadlineExceeded.
    /// </summary>
    public interface IRetryableGrpc
    {
        /// <summary>
        /// Attempt a gRPC call, retrying if the service is unavailable or the expected timeout is exceeded.
        /// </summary>
        /// <typeparam name="TRequest">The method request type.</typeparam>
        /// <typeparam name="TResponse">The method response type.</typeparam>
        /// <param name="call">The unary gRPC call to make.</param>
        /// <param name="request">The request data.</param>
        /// <param name="retry">The retry configuration, which controls how requests are retried.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the request.</param>
        /// <returns>The gRPC response.</returns>
        Task<TResponse> RetryableGrpcAsync<TRequest, TResponse>(
            Func<TRequest, Metadata?, DateTime?, CancellationToken, AsyncUnaryCall<TResponse>> call,
            TRequest request,
            GrpcRetryConfiguration retry,
            CancellationToken cancellationToken);

        /// <summary>
        /// Attempt a gRPC streaming call, retrying if the service is unavailable or if the server isn't delivering responses frequently enough.
        /// </summary>
        /// <typeparam name="TRequest">The method request type.</typeparam>
        /// <typeparam name="TResponse">The method response type.</typeparam>
        /// <param name="call">The unary gRPC call to make.</param>
        /// <param name="request">The request data.</param>
        /// <param name="retry">The retry configuration, which controls how requests are retried.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the request.</param>
        /// <returns>The gRPC response.</returns>
        IAsyncEnumerable<TResponse> RetryableStreamingGrpcAsync<TRequest, TResponse>(
            Func<TRequest, Metadata?, DateTime?, CancellationToken, AsyncServerStreamingCall<TResponse>> call,
            TRequest request,
            GrpcRetryConfiguration retry,
            CancellationToken cancellationToken);
    }
}
