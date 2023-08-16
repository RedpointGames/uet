namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    internal class WorkerSubpool
    {
        private readonly ILogger<WorkerSubpool> _logger;
        private readonly SemaphoreSlim _notifyReevaluationOfWorkers;
        private readonly AwaitableConcurrentQueue<IWorkerCore> _workerCoreQueue;
        internal readonly List<WorkerState> _workers;
        private readonly ConcurrentQueue<TaskApi.TaskApiClient> _workerExplicitAddQueue;
        private readonly SemaphoreSlim _workerExplicitAddComplete;

        private int _coresRequested;
        private int _coresReserved;

        internal WorkerSubpool(
            ILogger<WorkerSubpool> logger,
            SemaphoreSlim notifyReevaluationOfWorkers)
        {
            _logger = logger;
            _notifyReevaluationOfWorkers = notifyReevaluationOfWorkers;
            _workerCoreQueue = new AwaitableConcurrentQueue<IWorkerCore>();
            _workers = new List<WorkerState>();
            _workerExplicitAddQueue = new ConcurrentQueue<TaskApi.TaskApiClient>();
            _workerExplicitAddComplete = new SemaphoreSlim(0);

            _coresRequested = 0;
            _coresReserved = 0;
        }

        internal void IncrementCoresRequested()
        {
            Interlocked.Increment(ref _coresRequested);
            _logger.LogTrace($"[inc requested] cores requested: {_coresRequested}, cores reserved: {_coresReserved}");
        }

        internal void IncrementCoresReserved()
        {
            Interlocked.Increment(ref _coresReserved);
            _logger.LogTrace($"[inc reserved] cores requested: {_coresRequested}, cores reserved: {_coresReserved}");
        }

        internal void DecrementCoresRequested()
        {
            Interlocked.Decrement(ref _coresRequested);
            _logger.LogTrace($"[dec requested] cores requested: {_coresRequested}, cores reserved: {_coresReserved}");
        }

        internal void DecrementCoresReserved()
        {
            Interlocked.Decrement(ref _coresReserved);
            _logger.LogTrace($"[dec reserved] cores requested: {_coresRequested}, cores reserved: {_coresReserved}");
        }

        internal async Task RegisterWorkerAsync(TaskApi.TaskApiClient remoteClient)
        {
            _workerExplicitAddQueue.Enqueue(remoteClient);
            _notifyReevaluationOfWorkers.Release();
            await _workerExplicitAddComplete.WaitAsync();
        }

        internal async Task<IWorkerCore> ReserveCoreAsync(
            CancellationToken cancellationToken)
        {
            IncrementCoresRequested();
            _notifyReevaluationOfWorkers.Release();
            var didAcquire = false;
            try
            {
                IWorkerCore worker;
                do
                {
                    worker = await _workerCoreQueue.Dequeue(cancellationToken);
                    if (worker.Dead)
                    {
                        // When a worker core dies, it decrements the request
                        // count, since it normally happens when when the caller
                        // of ReserveCoreAsync disposes the worker core. In this case
                        // however, we're pulling a dead worker off the queue, which
                        // means we need to refresh that we still need a core to
                        // work with.
                        IncrementCoresRequested();
                        _notifyReevaluationOfWorkers.Release();
                    }
                }
                while (worker.Dead);
                didAcquire = true;
                return worker;
            }
            finally
            {
                if (!didAcquire)
                {
                    DecrementCoresRequested();
                }
            }
        }

        internal async Task ProcessWorkersAsync()
        {
            // Process any explicitly added workers.
            if (_workerExplicitAddQueue.TryDequeue(out var newRemoteClient))
            {
                _workers.Add(new WorkerState
                {
                    DisplayName = newRemoteClient?.ToString() ?? "(unnamed worker)",
                    Client = newRemoteClient!,
                });
                _workerExplicitAddComplete.Release();
            }

            // Determine how many remote workers we want to trying to
            // reserve a core from. We request cores from multiple workers
            // at once because we don't know when a core will become
            // available on a remote worker.
            const int forwardHeuristic = 4;
            var targetReserving = (_coresRequested - _coresReserved) * forwardHeuristic;

            // Determine the change in attempted reservations we need to
            // be making.
            var currentlyReserving = _workers
                .Count(x => x.PendingReservation != null);
            var changeInReserving = targetReserving - currentlyReserving;
            if (changeInReserving != 0)
            {
                _logger.LogInformation($"Worker pool is now trying to reserve {targetReserving} cores, a change of {changeInReserving}.");
            }

            // We need to reserve cores from more remote workers.
            if (changeInReserving > 0)
            {
                var reservationsInitiated = 0;
                foreach (var enumeratedWorker in _workers
                    .Where(x => x.PendingReservation == null)
                    // Prioritize the local worker first, since that will
                    // ensure we satisfy local-only tasks.
                    .OrderByDescending(x => x.IsLocalWorker ? 1 : 0)
                    // Then by workers that we've run more tasks on (as they're
                    // more likely to have tools and blobs already).
                    .ThenBy(x => x.TasksExecutedCount)
                    // Then by workers who have the oldest "timeouts" i.e.
                    // deprioritize those who reservation attempts have
                    // timed out recently.
                    .ThenBy(x => x.LastReservationTimeoutUtc))
                {
                    _logger.LogInformation($"Starting reservation on {enumeratedWorker}...");
                    var cancellationTokenSource = new CancellationTokenSource();
                    var worker = enumeratedWorker;
                    var pendingReservation = new WorkerStatePendingReservation();
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
                            _logger.LogInformation($"Requesting a core from {enumeratedWorker}...");
                            await pendingReservation.Request.RequestStream.WriteAsync(new ExecutionRequest
                            {
                                ReserveCore = new ReserveCoreRequest
                                {
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
                            _logger.LogInformation($"Obtained a core from {enumeratedWorker}, pushing it to the queue.");

                            // We've successfully reserved a core on this remote worker! Add it to the
                            // reservations list and push it into the queue of available cores.
                            var reservation = new WorkerStateReservation
                            {
                                Request = pendingReservation.Request,
                                Core = new DefaultWorkerCore(
                                    this,
                                    _logger,
                                    worker,
                                    pendingReservation.Request,
                                    pendingReservation.Request.ResponseStream.Current.ReserveCore.WorkerMachineName,
                                    pendingReservation.Request.ResponseStream.Current.ReserveCore.WorkerCoreNumber),
                                CancellationTokenSource = cancellationTokenSource,
                            };
                            pendingReservation.CancellationTokenSource = null;
                            IncrementCoresReserved();
                            await worker.AddReservationAsync(reservation);
                            _workerCoreQueue.Enqueue(reservation.Core);
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
                                _notifyReevaluationOfWorkers.Release();
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
                (changeInReserving < 0 && _coresRequested == 0))
            {
                // Prioritize releasing reservation attempts from workers
                // we have completed less tasks on.
                var reservationsCancelled = 0;
                foreach (var worker in _workers
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
                        _logger.LogInformation($"Cancelling pending reservation for {worker} because it's no longer needed.");
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
}
