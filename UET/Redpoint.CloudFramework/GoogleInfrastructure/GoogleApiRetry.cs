namespace Redpoint.CloudFramework.GoogleInfrastructure
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Repository;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a method for automatically retrying operations against
    /// Google APIs if the API throws an error that's retryable.
    /// </summary>
    public class GoogleApiRetry : IGoogleApiRetry
    {
        private static bool IsRecoverable(RpcException e, GoogleApiCallContext callContext, int attempts)
        {
            switch (e.Status.StatusCode)
            {
                case StatusCode.Aborted:
                    {
                        if (callContext == GoogleApiCallContext.PubSub)
                        {
                            return true;
                        }
                        else
                        {
                            // Can't retry for Aborted on Datastore.
                            return false;
                        }
                    }
                case StatusCode.Cancelled:
                    return callContext == GoogleApiCallContext.PubSub;
                case StatusCode.DeadlineExceeded:
                case StatusCode.Internal:
                    return attempts == 0;
                case StatusCode.ResourceExhausted:
                    return false;
                case StatusCode.Unknown:
                    return callContext == GoogleApiCallContext.PubSub;
                case StatusCode.Unavailable:
                    return true;
                default:
                    return false;
            }
        }

        public void DoRetryableOperation(GoogleApiCallContext callContext, ILogger logger, Action operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            var attempts = 0;
            var delay = 100;
            bool needsRetry;
            do
            {
                needsRetry = false;

                try
                {
                    operation();
                    return;
                }
                catch (RpcException ex) when (ex.IsContentionException())
                {
                    // Re-throw to allow the application to catch the content exception.
                    throw;
                }
                catch (RpcException ex) when (IsRecoverable(ex, callContext, attempts++))
                {
                    logger.LogWarning($"Got recoverable RPC exception: {ex.Status.StatusCode} \"{ex.Status.Detail}\", waiting ${delay}ms before retry...");

                    needsRetry = true;
                    Thread.Sleep(delay);
                    delay *= 2;
                    if (delay > 30000)
                    {
                        delay = 30000;
                    }
                }
            }
            while (needsRetry);
        }

        public T DoRetryableOperation<T>(GoogleApiCallContext callContext, ILogger logger, Func<T> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            var attempts = 0;
            var delay = 100;
            bool needsRetry;
            do
            {
                needsRetry = false;

                try
                {
                    return operation();
                }
                catch (RpcException ex) when (ex.IsContentionException())
                {
                    // Re-throw to allow the application to catch the content exception.
                    throw;
                }
                catch (RpcException ex) when (IsRecoverable(ex, callContext, attempts++))
                {
                    logger.LogWarning($"Got recoverable RPC exception: {ex.Status.StatusCode} \"{ex.Status.Detail}\", waiting ${delay}ms before retry...");

                    needsRetry = true;
                    Thread.Sleep(delay);
                    delay *= 2;
                    if (delay > 30000)
                    {
                        delay = 30000;
                    }
                }
            }
            while (needsRetry);

            throw new InvalidOperationException("Unexpected code reached in DoRetryableOperation");
        }

        public async Task DoRetryableOperationAsync(GoogleApiCallContext callContext, ILogger logger, Func<Task> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            var attempts = 0;
            var delay = 100;
            bool needsRetry;
            do
            {
                needsRetry = false;

                try
                {
                    await operation().ConfigureAwait(false);
                }
                catch (RpcException ex) when (ex.IsContentionException())
                {
                    // Re-throw to allow the application to catch the content exception.
                    throw;
                }
                catch (RpcException ex) when (IsRecoverable(ex, callContext, attempts++))
                {
                    logger.LogWarning($"Got recoverable RPC exception: {ex.Status.StatusCode} \"{ex.Status.Detail}\", waiting ${delay}ms before retry...");

                    needsRetry = true;
                    await Task.Delay(delay).ConfigureAwait(false);
                    delay *= 2;
                    if (delay > 30000)
                    {
                        delay = 30000;
                    }
                }
            }
            while (needsRetry);
        }

        public async Task<T> DoRetryableOperationAsync<T>(GoogleApiCallContext callContext, ILogger logger, Func<Task<T>> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            var attempts = 0;
            var delay = 100;
            bool needsRetry;
            do
            {
                needsRetry = false;

                try
                {
                    return await operation().ConfigureAwait(false);
                }
                catch (RpcException ex) when (ex.IsContentionException())
                {
                    // Re-throw to allow the application to catch the content exception.
                    throw;
                }
                catch (RpcException ex) when (IsRecoverable(ex, callContext, attempts++))
                {
                    logger.LogWarning($"Got recoverable RPC exception: {ex.Status.StatusCode} \"{ex.Status.Detail}\", waiting ${delay}ms before retry...");

                    needsRetry = true;
                    await Task.Delay(delay).ConfigureAwait(false);
                    delay *= 2;
                    if (delay > 30000)
                    {
                        delay = 30000;
                    }
                }
            }
            while (needsRetry);

            throw new InvalidOperationException("Unexpected code reached in DoRetryableOperationAsync");
        }
    }
}
