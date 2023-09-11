namespace Redpoint.OpenGE.Component.Worker
{
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    internal sealed class ConnectionIdleTracker : IDisposable
    {
        private CancellationTokenSource _idledTooLong;
        private readonly CancellationTokenSource _waitingForRequest;
        private int _idleTimeoutMilliseconds;
        private readonly Func<ConnectionIdleEventOutcome>? _considerIdleEvent;
        private readonly Concurrency.Semaphore _threadSafety;
        private CancellationTokenSource _cancelIdling;
        private Task? _idleCheckingTask;

        [SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "The cancellation token is intentionally the first parameter.")]
        public ConnectionIdleTracker(
            CancellationToken clientInitiatedRequestCancellation,
            int idleTimeoutMilliseconds,
            Func<ConnectionIdleEventOutcome>? considerIdleEvent)
        {
            _waitingForRequest = CancellationTokenSource.CreateLinkedTokenSource(
                clientInitiatedRequestCancellation);
            _cancelIdling = new CancellationTokenSource();
            _idleCheckingTask = null;
            _idleTimeoutMilliseconds = idleTimeoutMilliseconds;
            _considerIdleEvent = considerIdleEvent;
            _threadSafety = new Concurrency.Semaphore(1);
            _idledTooLong = new CancellationTokenSource();
            ResetIdledTooLong();
        }

        private void ResetIdledTooLong()
        {
            _idledTooLong = new CancellationTokenSource();
            _idledTooLong.Token.Register(() =>
            {
                var outcome = ConnectionIdleEventOutcome.Idled;
                if (_considerIdleEvent != null)
                {
                    outcome = _considerIdleEvent();
                }
                if (outcome == ConnectionIdleEventOutcome.Idled)
                {
                    _waitingForRequest.Cancel();
                }
                else if (outcome == ConnectionIdleEventOutcome.ResetIdleTimer)
                {
                    StopIdling();
                    ResetIdledTooLong();
                    StartIdling();
                }
                else
                {
                    throw new NotSupportedException();
                }
            });
        }

        public CancellationToken CancellationToken => _waitingForRequest.Token;

        public void StartIdling(int? newIdleTimeoutMilliseconds = null)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                return;
            }
            _threadSafety.Wait(CancellationToken.None);
            try
            {
                if (newIdleTimeoutMilliseconds != null)
                {
                    _idleTimeoutMilliseconds = newIdleTimeoutMilliseconds.Value;
                }
                if (_idleCheckingTask == null ||
                    _idleCheckingTask.IsCompleted)
                {
                    var cancelIdlingToken = _cancelIdling!.Token;
                    _idleCheckingTask = Task.Run(async () =>
                    {
                        if (!Debugger.IsAttached)
                        {
                            await Task.Delay(_idleTimeoutMilliseconds, cancelIdlingToken).ConfigureAwait(false);
                            _idledTooLong.Cancel();
                        }
                    });
                }
            }
            finally
            {
                _threadSafety.Release();
            }
        }

        public void StopIdling()
        {
            _threadSafety.Wait(CancellationToken.None);
            try
            {
                _cancelIdling!.Cancel();
                _cancelIdling = new CancellationTokenSource();
            }
            finally
            {
                _threadSafety.Release();
            }
        }

        public void Dispose()
        {
            _idledTooLong.Dispose();
            _waitingForRequest.Dispose();
            _cancelIdling.Dispose();
        }
    }
}
