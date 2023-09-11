namespace Redpoint.GrpcPipes
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    internal sealed class DefaultRetryableGrpc : IRetryableGrpc
    {
        private readonly ILogger<DefaultRetryableGrpc> _logger;

        public DefaultRetryableGrpc(ILogger<DefaultRetryableGrpc> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> RetryableGrpcAsync<TRequest, TResponse>(
            Func<TRequest, Metadata?, DateTime?, CancellationToken, AsyncUnaryCall<TResponse>> call,
            TRequest request,
            GrpcRetryConfiguration retry,
            CancellationToken cancellationToken)
        {
            var backoff = (float)retry.InitialBackoffMilliseconds;
            var maxAttempts = retry.MaxAttempts;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    return await call(request, null, DateTime.UtcNow.Add(retry.RequestTimeout), cancellationToken);
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
                {
                    if (attempt != maxAttempts - 1)
                    {
                        _logger.LogTrace($"gRPC call failed with status code {ex.StatusCode} on attempt {attempt + 1}. Retrying in {backoff}ms...");
                        await Task.Delay((int)backoff, cancellationToken).ConfigureAwait(false);
                        backoff *= retry.ExponentialBackoffMultiplier;
                        if (backoff > retry.MaximumBackoffMilliseconds)
                        {
                            backoff = retry.MaximumBackoffMilliseconds;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            throw new RpcException(new Status(StatusCode.Unavailable, $"gRPC call failed in {maxAttempts} attempts."));
        }

        public async IAsyncEnumerable<TResponse> RetryableStreamingGrpcAsync<TRequest, TResponse>(
            Func<TRequest, Metadata?, DateTime?, CancellationToken, AsyncServerStreamingCall<TResponse>> call,
            TRequest request,
            GrpcRetryConfiguration retry,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var backoff = (float)retry.InitialBackoffMilliseconds;
            var maxAttempts = retry.MaxAttempts;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                AsyncServerStreamingCall<TResponse>? response;
                bool hasMessage;
                var currentTimeout = new CancellationTokenSource(retry.RequestTimeout);

                // Do the initial connection and get the first message.
                try
                {
                    response = call(request, null, null, cancellationToken);
                    hasMessage = await response.ResponseStream.MoveNext(currentTimeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (currentTimeout.IsCancellationRequested)
                {
                    if (attempt != maxAttempts - 1)
                    {
                        _logger.LogTrace($"gRPC streaming call failed as no response was received within {retry.RequestTimeout.TotalMilliseconds}ms on attempt {attempt + 1}. Retrying in {backoff}ms...");
                        await Task.Delay((int)backoff, cancellationToken).ConfigureAwait(false);
                        backoff *= retry.ExponentialBackoffMultiplier;
                        if (backoff > retry.MaximumBackoffMilliseconds)
                        {
                            backoff = retry.MaximumBackoffMilliseconds;
                        }
                        continue;
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
                {
                    if (attempt != maxAttempts - 1)
                    {
                        _logger.LogTrace($"gRPC streaming call failed with status code {ex.StatusCode} on attempt {attempt + 1}. Retrying in {backoff}ms...");
                        await Task.Delay((int)backoff, cancellationToken).ConfigureAwait(false);
                        backoff *= retry.ExponentialBackoffMultiplier;
                        if (backoff > retry.MaximumBackoffMilliseconds)
                        {
                            backoff = retry.MaximumBackoffMilliseconds;
                        }
                        continue;
                    }
                    else
                    {
                        throw;
                    }
                }

                // If we have no messages, return.
                if (!hasMessage || response == null)
                {
                    yield break;
                }

                // Otherwise, while we have messages, yield them.
                var needsRetry = false;
                do
                {
                    yield return response.ResponseStream.Current;

                    // Get the next message.
                    var idleTimeout = retry.IdleTimeout ?? retry.RequestTimeout;
                    currentTimeout = new CancellationTokenSource(idleTimeout);
                    try
                    {
                        hasMessage = await response.ResponseStream.MoveNext(currentTimeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (currentTimeout.IsCancellationRequested)
                    {
                        if (attempt != maxAttempts - 1)
                        {
                            _logger.LogTrace($"gRPC streaming call failed as no response was received within {idleTimeout.TotalMilliseconds}ms on attempt {attempt + 1}. Retrying in {backoff}ms...");
                            await Task.Delay((int)backoff, cancellationToken).ConfigureAwait(false);
                            backoff *= retry.ExponentialBackoffMultiplier;
                            if (backoff > retry.MaximumBackoffMilliseconds)
                            {
                                backoff = retry.MaximumBackoffMilliseconds;
                            }
                            needsRetry = true;
                            break;
                        }
                        else
                        {
                            throw;
                        }
                    }
                    catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
                    {
                        if (attempt != maxAttempts - 1)
                        {
                            _logger.LogTrace($"gRPC streaming call failed with status code {ex.StatusCode} on attempt {attempt + 1}. Retrying in {backoff}ms...");
                            await Task.Delay((int)backoff, cancellationToken).ConfigureAwait(false);
                            backoff *= retry.ExponentialBackoffMultiplier;
                            if (backoff > retry.MaximumBackoffMilliseconds)
                            {
                                backoff = retry.MaximumBackoffMilliseconds;
                            }
                            needsRetry = true;
                            break;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                while (hasMessage);

                // If we need to retry the call, loop around in attempts again.
                if (needsRetry)
                {
                    continue;
                }

                // Now we have no more messages.
                yield break;
            }

            // We ran out of attempts.
            throw new RpcException(new Status(StatusCode.Unavailable, $"gRPC streaming call failed in {maxAttempts} attempts."));
        }
    }
}
