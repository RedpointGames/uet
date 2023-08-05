namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Protocol;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultWorkerPool : IWorkerPool
    {
        private readonly ILogger<DefaultWorkerPool> _logger;
        private readonly SemaphoreSlim _notifyReevaluationOfWorkers;
        private readonly CancellationTokenSource _disposedCts;
        internal readonly WorkerSubpool _localSubpool;
        internal readonly WorkerSubpool _remoteSubpool;

        private readonly Task _workersProcessingTask;

        public DefaultWorkerPool(
            ILogger<DefaultWorkerPool> logger,
            ILogger<WorkerSubpool> subpoolLogger,
            TaskApi.TaskApiClient? localWorker)
        {
            _logger = logger;
            _notifyReevaluationOfWorkers = new SemaphoreSlim(0);
            _disposedCts = new CancellationTokenSource();
            _localSubpool = new WorkerSubpool(
                subpoolLogger,
                _notifyReevaluationOfWorkers);
            _remoteSubpool = new WorkerSubpool(
                subpoolLogger,
                _notifyReevaluationOfWorkers);

            if (localWorker != null)
            {
                _localSubpool._workers.Add(new WorkerState
                {
                    Client = localWorker,
                    IsLocalWorker = true,
                });
                _remoteSubpool._workers.Add(new WorkerState
                {
                    Client = localWorker,
                    IsLocalWorker = true,
                });
            }

            _workersProcessingTask = Task.Run(PeriodicallyProcessWorkers);
        }


        private async Task PeriodicallyProcessWorkers()
        {
            while (!_disposedCts.IsCancellationRequested)
            {
                // Reprocess remote workers state either:
                // - Every 10 seconds, or
                // - When the notification semaphore tells us we need to reprocess now.
                var timingCts = CancellationTokenSource.CreateLinkedTokenSource(
                    new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token,
                    _disposedCts.Token);
                try
                {
                    await _notifyReevaluationOfWorkers.WaitAsync(timingCts.Token);
                }
                catch (OperationCanceledException) when (timingCts.IsCancellationRequested)
                {
                }
                if (_disposedCts.IsCancellationRequested)
                {
                    // The worker pool is disposing.
                    return;
                }

                await _localSubpool.ProcessWorkersAsync();
                await _remoteSubpool.ProcessWorkersAsync();
            }
        }

        public Task<IWorkerCore> ReserveCoreAsync(
            bool requireLocalCore,
            CancellationToken cancellationToken)
        {
            if (requireLocalCore)
            {
                return _localSubpool.ReserveCoreAsync(cancellationToken);
            }
            else
            {
                return _remoteSubpool.ReserveCoreAsync(cancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _disposedCts.Cancel();
            try
            {
                await _workersProcessingTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
