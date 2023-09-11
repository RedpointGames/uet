namespace Redpoint.Concurrency
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Invokes a callback with the result when the first operation returns a
    /// result. Unlike <see cref="Task.WhenAny{TResult}(Task{TResult}[])"/>, this
    /// only waits for the first result to be returned.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    public class FirstPastThePost<TResult> where TResult : class
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private long _scheduledOperations;
        private bool _hasResult;
        private readonly Semaphore _resultSemaphore;
        private readonly Func<TResult?, Task> _onResult;

        /// <summary>
        /// Constructs a new <see cref="FirstPastThePost{TResult}"/> instance.
        /// </summary>
        /// <param name="cancellationTokenSource">The cancellation token source that will be cancelled once a result has been received.</param>
        /// <param name="scheduledOperations">The number of operations scheduled.</param>
        /// <param name="onResult">The callback to issue once a result is received, or when all operations have returned no results.</param>
        public FirstPastThePost(
            CancellationTokenSource cancellationTokenSource,
            long scheduledOperations,
            Func<TResult?, Task> onResult)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _scheduledOperations = scheduledOperations;
            _hasResult = false;
            _resultSemaphore = new Semaphore(1);
            _onResult = onResult;
        }

        /// <summary>
        /// Whether this instance has received a result yet.
        /// </summary>
        public bool HasReceivedResult => _hasResult;

        /// <summary>
        /// Sets the number of scheduled operations.
        /// </summary>
        public async Task UpdateScheduledOperationsAsync(long scheduledOperations)
        {
            await _resultSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_hasResult)
                {
                    throw new InvalidOperationException("Result already returned.");
                }

                _scheduledOperations = scheduledOperations;
            }
            finally
            {
                _resultSemaphore.Release();
            }
        }

        /// <summary>
        /// Sets the result for this instance.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>The awaitable task.</returns>
        /// <exception cref="InvalidOperationException">If <see cref="ReceiveResultAsync(TResult)"/> or <see cref="ReceiveNoResultAsync"/> has been called too many times.</exception>
        public async Task ReceiveResultAsync(TResult result)
        {
            var broadcastResult = false;
            await _resultSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                _scheduledOperations--;
                if (_scheduledOperations < 0)
                {
                    throw new InvalidOperationException("Got more ReceiveResultAsync/ReceiveNoResultAsync than expected.");
                }

                if (_hasResult)
                {
                    return;
                }

                _hasResult = true;
                _cancellationTokenSource.Cancel();
                broadcastResult = true;
            }
            finally
            {
                _resultSemaphore.Release();
            }

            if (broadcastResult)
            {
                await _onResult(result).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Indicates that an operation did not return a result.
        /// </summary>
        /// <returns>The awaitable task.</returns>
        /// <exception cref="InvalidOperationException">If <see cref="ReceiveResultAsync(TResult)"/> or <see cref="ReceiveNoResultAsync"/> has been called too many times.</exception>
        public async Task ReceiveNoResultAsync()
        {
            var broadcastResult = false;
            await _resultSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                _scheduledOperations--;
                if (_scheduledOperations < 0)
                {
                    throw new InvalidOperationException("Got more ReceiveResultAsync/ReceiveNoResultAsync than expected.");
                }

                if (_hasResult)
                {
                    return;
                }

                if (_scheduledOperations == 0)
                {
                    // We're broadcasting nothing, because no task returned
                    // a result.
                    _hasResult = true;
                    _cancellationTokenSource.Cancel();
                    broadcastResult = true;
                }
            }
            finally
            {
                _resultSemaphore.Release();
            }

            if (broadcastResult)
            {
                await _onResult(null).ConfigureAwait(false);
            }
        }
    }
}
