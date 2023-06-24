namespace Redpoint.Uefs.Daemon.Transactional
{
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

        internal IAsyncDisposable GetDisposableForInitiallyRegisteredListener()
        {
            return new ReleasableListener(this, _initialListener);
        }

        protected class ReleasableListener : IAsyncDisposable
        {
            private readonly AbstractTransaction<TRequest, TListenerDelegate> _transaction;
            private readonly TListenerDelegate _listener;

            public ReleasableListener(AbstractTransaction<TRequest, TListenerDelegate> transaction, TListenerDelegate listener)
            {
                _transaction = transaction;
                _listener = listener;
            }

            public async ValueTask DisposeAsync()
            {
                _transaction._listeners.TryRemove(_listener, out _);
                if (_transaction._listeners.Count == 0 && !_transaction._backgroundable)
                {
                    // If there is no-one interested in the transaction any more (because all
                    // the listeners are gone), and the transaction is not backgroundable (i.e.
                    // it was started with NoWait), then also cancel the transaction.
                    _transaction._executorCancellationTokenSource.Cancel();

                    // Wait for the executor to bubble up the cancellation so that the last
                    // DisposeAsync will wait until the executor stops all work.
                    await _transaction._executorCompleteSemaphore.WaitAsync();
                    _transaction._executorCompleteSemaphore.Release();

                    // If we threw an exception and it wasn't a cancellation
                    // exception, then re-throw it here because it means the
                    // executor failed in some unexpected way and we'd like that
                    // to propagate at least somewhere in the application (instead
                    // of being lost from the background task).
                    if (_transaction._thrownException != null &&
                        !(_transaction._thrownException.SourceException is OperationCanceledException))
                    {
                        _transaction._thrownException.Throw();
                    }
                }
            }
        }

        public IAsyncDisposable RegisterListener(TListenerDelegate listenerDelegate)
        {
            _listeners.TryAdd(listenerDelegate, true);
            return new ReleasableListener(this, listenerDelegate);
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
                        await InvokeListenerAsync(kv.Key, pollingResponse);
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

        public async Task WaitForCompletionAsync(CancellationToken cancellationToken)
        {
            await _executorCompleteSemaphore.WaitAsync(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _executorCancellationTokenSource.Token).Token);
            _executorCompleteSemaphore.Release();
            if (_thrownException != null)
            {
                _thrownException.Throw();
            }
        }
    }
}
