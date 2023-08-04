namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultWorkerPool : IWorkerPool
    {
        private readonly ILogger<DefaultWorkerPool> _logger;
        private readonly TaskApi.TaskApiClient? _localWorker;
        private readonly SemaphoreSlim _notifyReevaluationOfRemoteWorkers;
        private readonly CancellationTokenSource _disposedCts;
        private readonly SemaphoreSlim _remoteWorkerCoreAvailable;
        private readonly ConcurrentQueue<IWorkerCore> _remoteWorkerCoreQueue;
        private readonly List<RemoteWorkerState> _remoteWorkers;
        private readonly ConcurrentQueue<TaskApi.TaskApiClient> _remoteWorkerExplicitAddQueue;
        private readonly SemaphoreSlim _remoteWorkerExplicitAddComplete;

        internal int _remoteCoresRequested;
        internal int _remoteCoresReserved;

        private readonly Task _remoteWorkersProcessingTask;

        public DefaultWorkerPool(
            ILogger<DefaultWorkerPool> logger,
            TaskApi.TaskApiClient? localWorker)
        {
            _notifyReevaluationOfRemoteWorkers = new SemaphoreSlim(0);
            _logger = logger;
            _localWorker = localWorker;
            _disposedCts = new CancellationTokenSource();
            _remoteWorkerCoreAvailable = new SemaphoreSlim(0);
            _remoteWorkerCoreQueue = new ConcurrentQueue<IWorkerCore>();
            _remoteWorkers = new List<RemoteWorkerState>();
            _remoteWorkerExplicitAddQueue = new ConcurrentQueue<TaskApi.TaskApiClient>();
            _remoteWorkerExplicitAddComplete = new SemaphoreSlim(0);

            _remoteCoresRequested = 0;
            _remoteCoresReserved = 0;

            _remoteWorkersProcessingTask = Task.Run(PeriodicallyProcessRemoteWorkers);
        }

        internal async Task RegisterRemoteWorkerAsync(TaskApi.TaskApiClient remoteClient)
        {
            _remoteWorkerExplicitAddQueue.Enqueue(remoteClient);
            _notifyReevaluationOfRemoteWorkers.Release();
            await _remoteWorkerExplicitAddComplete.WaitAsync();
        }

        private async Task PeriodicallyProcessRemoteWorkers()
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
                    await _notifyReevaluationOfRemoteWorkers.WaitAsync(timingCts.Token);
                }
                catch (OperationCanceledException) when (timingCts.IsCancellationRequested)
                {
                }
                if (_disposedCts.IsCancellationRequested)
                {
                    // The worker pool is disposing.
                    return;
                }

                // Process any explicitly added workers.
                if (_remoteWorkerExplicitAddQueue.TryDequeue(out var newRemoteClient))
                {
                    _remoteWorkers.Add(new RemoteWorkerState
                    {
                        Client = newRemoteClient,
                    });
                    _remoteWorkerExplicitAddComplete.Release();
                }

                // Determine how many remote workers we want to trying to
                // reserve a core from. We request cores from multiple workers
                // at once because we don't know when a core will become
                // available on a remote worker.
                const int forwardHeuristic = 4;
                var targetReserving = (_remoteCoresRequested - _remoteCoresReserved) * forwardHeuristic;

                // Determine the change in attempted reservations we need to
                // be making.
                var currentlyReserving = _remoteWorkers
                    .Count(x => x.PendingReservation != null);
                var changeInReserving = targetReserving - currentlyReserving;
                if (changeInReserving != 0)
                {
                    _logger.LogTrace($"Worker pool is now trying to reserve {targetReserving} cores, a change of {changeInReserving}.");
                }

                // We need to reserve cores from more remote workers.
                if (changeInReserving > 0)
                {
                    // Prioritize taking reservations on workers that we've
                    // run more tasks on (as they're more likely to have
                    // tools and blobs already).
                    var reservationsInitiated = 0;
                    foreach (var enumeratedWorker in _remoteWorkers
                        .Where(x => x.PendingReservation == null)
                        .OrderByDescending(x => x.TasksExecutedCount)
                        // Then by workers who have the oldest "timeouts" i.e.
                        // deprioritize those who reservation attempts have
                        // timed out recently.
                        .ThenBy(x => x.LastReservationTimeoutUtc))
                    {
                        var cancellationTokenSource = new CancellationTokenSource();
                        var worker = enumeratedWorker;
                        var pendingReservation = new RemoteWorkerStatePendingReservation();
                        pendingReservation.CancellationTokenSource = cancellationTokenSource;
                        pendingReservation.Task = Task.Run(async () =>
                        {
                            var cancellationToken = cancellationTokenSource.Token;
                            var didReserve = false;
                            var shouldReprocessRemoteWorkers = false;
                            try
                            {
                                pendingReservation.Request = worker.Client.ReserveCoreAndExecute(
                                    cancellationToken: cancellationToken);

                                // Send the request to reserve a core.
                                await pendingReservation.Request.RequestStream.WriteAsync(new ExecutionRequest
                                {
                                    ReserveCore = new ReserveCoreRequest
                                    {
                                        WaitForever = true,
                                    }
                                }, cancellationToken);

                                // Get the core reserved response. This operation will cease if the 
                                // cancellation token is cancelled before the reservation is made.
                                if (!await pendingReservation.Request.ResponseStream.MoveNext(cancellationToken))
                                {
                                    // The server disconnected from us without reserving a core. This
                                    // can happen if the remote worker is e.g. shutting down.
                                    shouldReprocessRemoteWorkers = true;
                                    return;
                                }
                                if (pendingReservation.Request.ResponseStream.Current.ResponseCase != ExecutionResponse.ResponseOneofCase.ReserveCore)
                                {
                                    // The server gave us a response that wasn't core reservation. This
                                    // is unexpected and we have no recovery from this.
                                    shouldReprocessRemoteWorkers = true;
                                    return;
                                }

                                // We've successfully reserved a core on this remote worker! Add it to the
                                // reservations list and push it into the queue of available cores.
                                var reservation = new RemoteWorkerStateReservation
                                {
                                    Request = pendingReservation.Request,
                                    Core = new RemoteWorkerCore(this, worker, pendingReservation.Request),
                                    CancellationTokenSource = cancellationTokenSource,
                                };
                                pendingReservation.CancellationTokenSource = null;
                                Interlocked.Increment(ref _remoteCoresReserved);
                                await worker.AddReservationAsync(reservation);
                                _remoteWorkerCoreQueue.Enqueue(reservation.Core);
                                _remoteWorkerCoreAvailable.Release();
                                didReserve = true;

                                // We want to reprocess remote workers after we finish here, since the pool
                                // may be able to cease reserving cores that we no longer need.
                                shouldReprocessRemoteWorkers = true;
                            }
                            finally
                            {
                                worker.PendingReservation = null;
                                if (!didReserve)
                                {
                                    worker.LastReservationTimeoutUtc = DateTimeOffset.UtcNow;
                                }
                                if (shouldReprocessRemoteWorkers)
                                {
                                    // Tell the remote worker loop that it might need to reschedule
                                    // reservations of workers.
                                    _notifyReevaluationOfRemoteWorkers.Release();
                                }
                            }
                        });
                        worker.PendingReservation = pendingReservation;
                        worker.LastReservationRequestStartUtc = DateTimeOffset.UtcNow;
                        reservationsInitiated++;
                        if (reservationsInitiated >= changeInReserving)
                        {
                            break;
                        }
                    }
                }
                // We don't need to be reserving as many cores as we are. We
                // don't scale down quickly unless we're actually aiming for
                // zero.
                else if (
                    changeInReserving < -forwardHeuristic ||
                    (changeInReserving < 0 && _remoteCoresRequested == 0))
                {
                    // Prioritize releasing reservation attempts from workers
                    // we have completed less tasks on.
                    var reservationsCancelled = 0;
                    foreach (var worker in _remoteWorkers
                        .Where(x => x.PendingReservation != null)
                        .OrderBy(x => x.TasksExecutedCount))
                    {
                        // Protect against race conditions by getting a reference to
                        // the pending reservation (so we don't run into issues if
                        // worker.PendingReservation becomes null while running this
                        // logic).
                        var pendingReservation = worker.PendingReservation;
                        if (pendingReservation != null)
                        {
                            pendingReservation.CancellationTokenSource?.Cancel();
                            try
                            {
                                await pendingReservation.Task!;
                            }
                            catch (OperationCanceledException)
                            {
                            }
                        }

                        // Set the worker as no longer trying to reserve.
                        worker.PendingReservation = null;
                        reservationsCancelled++;
                        if (reservationsCancelled >= -changeInReserving)
                        {
                            break;
                        }
                    }
                }
            }
        }

        public Task<IWorkerCore> ReserveLocalCoreAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<IWorkerCore> ReserveRemoteOrLocalCoreAsync(
            CancellationToken cancellationToken)
        {
            _remoteCoresRequested++;
            _notifyReevaluationOfRemoteWorkers.Release();
            var didAcquire = false;
            try
            {
                await _remoteWorkerCoreAvailable.WaitAsync(cancellationToken);
                if (_remoteWorkerCoreQueue.TryDequeue(out var worker))
                {
                    didAcquire = true;
                    return worker;
                }
                throw new InvalidOperationException("Worker queue indicated remote worker was available, but none could be pulled from the concurrent queue.");
            }
            finally
            {
                if (!didAcquire)
                {
                    _remoteCoresRequested--;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _disposedCts.Cancel();
            try
            {
                await _remoteWorkersProcessingTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
