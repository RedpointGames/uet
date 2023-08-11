namespace Redpoint.OpenGE.Component.Worker
{
    using System.Diagnostics;
    using System.Threading.Tasks;

    internal class ConnectionIdleTracker
    {
        private readonly CancellationTokenSource _idledTooLong;
        private readonly CancellationTokenSource _waitingForRequest;
        private int _idleTimeoutMilliseconds;
        private readonly SemaphoreSlim _threadSafety;
        private CancellationTokenSource _cancelIdling;
        private Task? _idleCheckingTask;

        public ConnectionIdleTracker(
            CancellationToken clientInitiatedRequestCancellation,
            int idleTimeoutMilliseconds)
        {
            _idledTooLong = new CancellationTokenSource();
            _waitingForRequest = CancellationTokenSource.CreateLinkedTokenSource(
                clientInitiatedRequestCancellation,
                _idledTooLong.Token);
            _cancelIdling = new CancellationTokenSource();
            _idleCheckingTask = null;
            _idleTimeoutMilliseconds = idleTimeoutMilliseconds;
            _threadSafety = new SemaphoreSlim(1);
        }

        public CancellationToken CancellationToken => _waitingForRequest.Token;

        public void StartIdling(int? newIdleTimeoutMilliseconds = null)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                return;
            }
            _threadSafety.Wait();
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
                            await Task.Delay(_idleTimeoutMilliseconds, cancelIdlingToken);
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
            _threadSafety.Wait();
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
    }
}
