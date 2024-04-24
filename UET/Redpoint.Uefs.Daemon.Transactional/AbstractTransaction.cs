namespace Redpoint.Uefs.Daemon.Transactional
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using System;
    using System.Collections.Concurrent;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;

    internal abstract class AbstractTransaction<TRequest, TListenerDelegate> :
        ITransaction
        where TRequest : class, ITransactionRequest
        where TListenerDelegate : notnull
    {
        protected readonly ConcurrentDictionary<TListenerDelegate, bool> _listeners;
        private readonly TListenerDelegate _initialListener;
        private readonly CancellationTokenSource _executorCancellationTokenSource;
        private readonly bool _backgroundable;
        private readonly SemaphoreSlim _executorCompleteSemaphore;
        private Task? _currentPollingStatusFlush;
        private bool _hasPendingPollingStatusFlush;
        internal ExceptionDispatchInfo? _thrownException;

        public AbstractTransaction(
            TRequest request,
            TListenerDelegate initialListener,
            CancellationTokenSource executorCancellationTokenSource,
            bool backgroundable,
            SemaphoreSlim executorCompleteSemaphore)
        {
            _listeners = new ConcurrentDictionary<TListenerDelegate, bool>();
            _listeners.TryAdd(initialListener, true);
            _executorCancellationTokenSource = executorCancellationTokenSource;
            _backgroundable = backgroundable;
            _executorCompleteSemaphore = executorCompleteSemaphore;
            _currentPollingStatusFlush = null;
            _hasPendingPollingStatusFlush = false;

            Request = request;
            _initialListener = initialListener;
            LatestPollingResponse = new PollingResponse
            {
                Type = PollingResponseType.Backgrounded,
            };
        }

        public object RequestUnknown => Request;

        public TRequest Request { get; }

        public PollingResponse LatestPollingResponse { get; private set; }

        internal IAsyncDisposable GetDisposableForInitiallyRegisteredListener(ILogger logger)
        {
            return new ReleasableListener(this, _initialListener, logger);
        }

        protected sealed class ReleasableListener : IAsyncDisposable
        {
            private readonly AbstractTransaction<TRequest, TListenerDelegate> _transaction;
            private readonly TListenerDelegate _listener;
            private readonly ILogger _logger;

            public ReleasableListener(
                AbstractTransaction<TRequest, TListenerDelegate> transaction,
                TListenerDelegate listener,
                ILogger logger)
            {
                _transaction = transaction;
                _listener = listener;
                _logger = logger;
            }

            public async ValueTask DisposeAsync()
            {
                _logger.LogInformation("Transaction listener is being disposed, removing from listeners list.");
                _transaction._listeners.TryRemove(_listener, out _);

                _logger.LogInformation("Checking if the list of listeners is empty and the transaction is not backgroundable.");
                if (_transaction._listeners.IsEmpty && !_transaction._backgroundable)
                {
                    // If there is no-one interested in the transaction any more (because all
                    // the listeners are gone), and the transaction is not backgroundable (i.e.
                    // it was started with NoWait), then also cancel the transaction.
                    _logger.LogInformation("Cancelling the transaction as it is not backgroundable.");
                    _transaction._executorCancellationTokenSource.Cancel();

                    // Wait for the executor to bubble up the cancellation so that the last
                    // DisposeAsync will wait until the executor stops all work.
                    _logger.LogInformation("Waiting for the execute to bubble up the cancellation.");
                    await _transaction._executorCompleteSemaphore.WaitAsync().ConfigureAwait(false);
                    _transaction._executorCompleteSemaphore.Release();

                    // If we threw an exception and it wasn't a cancellation
                    // exception, then re-throw it here because it means the
                    // executor failed in some unexpected way and we'd like that
                    // to propagate at least somewhere in the application (instead
                    // of being lost from the background task).
                    _logger.LogInformation("Checking to see if there is a thrown exception on the stack.");
                    if (_transaction._thrownException != null &&
                        !(_transaction._thrownException.SourceException is OperationCanceledException))
                    {
                        _logger.LogInformation("Re-throwing the thrown exception.");
                        _transaction._thrownException.Throw();
                    }
                }
                else
                {
                    _logger.LogInformation("Listeners are not empty or transaction is not backgroundable.");
                }
            }
        }

        public IAsyncDisposable RegisterListener(TListenerDelegate listenerDelegate, ILogger logger)
        {
            _listeners.TryAdd(listenerDelegate, true);
            return new ReleasableListener(this, listenerDelegate, logger);
        }

        protected abstract Task InvokeListenerAsync(TListenerDelegate @delegate, PollingResponse response);

        private void SchedulePollingResponseTask(PollingResponse pollingResponse)
        {
            if (_currentPollingStatusFlush != null)
            {
                _hasPendingPollingStatusFlush = true;
            }
            else
            {
                _currentPollingStatusFlush = Task.Run(async () =>
                {
                    foreach (var kv in _listeners)
                    {
                        await InvokeListenerAsync(kv.Key, pollingResponse).ConfigureAwait(false);
                    }

                    await Task.Yield();
                    _currentPollingStatusFlush = null;

                    if (_hasPendingPollingStatusFlush)
                    {
                        _hasPendingPollingStatusFlush = false;
                        SchedulePollingResponseTask(LatestPollingResponse);
                    }
                });
            }
        }

        public void UpdatePollingResponse(PollingResponse pollingResponse)
        {
            LatestPollingResponse = pollingResponse;
            SchedulePollingResponseTask(LatestPollingResponse);
        }

        public async Task WaitForCompletionAsync(ILogger logger, CancellationToken cancellationToken)
        {
            logger.LogInformation("Waiting for transaction completion on the executor semaphore.");
            await _executorCompleteSemaphore.WaitAsync(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _executorCancellationTokenSource.Token).Token).ConfigureAwait(false);

            logger.LogInformation("Releasing transaction completion semaphore again.");
            _executorCompleteSemaphore.Release();

            logger.LogInformation("Checking to see if there is a thrown exception.");
            if (_thrownException != null)
            {
                logger.LogInformation("Re-throwing the thrown exception from the transaction executor.");
                _thrownException.Throw();
            }
        }
    }
}
