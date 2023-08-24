namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Concurrent;

    internal class WorkerState
    {
        private readonly List<WorkerStateReservation> _reservations = new List<WorkerStateReservation>();
        private readonly SemaphoreSlim _reservationsLock = new SemaphoreSlim(1);

        /// <summary>
        /// The display name for this worker.
        /// </summary>
        public required string DisplayName { get; init; }

        /// <summary>
        /// The unique ID for this worker. If a request
        /// to add a worker to the pool is made and a worker
        /// with the same unique ID is already present, the
        /// request will be ignored. This prevents the same
        /// machine being added twice through two different
        /// IP addresses.
        /// </summary>
        public required string UniqueId { get; init; }

        public override string ToString()
        {
            return DisplayName;
        }

        /// <summary>
        /// The client for the remote worker that we will communicate on.
        /// </summary>
        public required TaskApi.TaskApiClient Client { get; init; }

        /// <summary>
        /// If true, this worker is on the local machine and can be used to execute tasks that do not support remoting.
        /// </summary>
        public bool IsLocalWorker { get; init; }

        /// <summary>
        /// The pending reservation process, if there is one.
        /// </summary>
        public WorkerStatePendingReservation? PendingReservation { get; set; }

        /// <summary>
        /// The last time that we started requesting a reservation from this worker.
        /// </summary>
        public DateTimeOffset LastReservationRequestStartUtc { get; set; } = DateTimeOffset.MinValue;

        /// <summary>
        /// The last time we attempted to reserve a core, but the call was cancelled or timed out
        /// before the reservation could be made.
        /// </summary>
        public DateTimeOffset LastReservationTimeoutUtc { get; set; } = DateTimeOffset.MinValue;

        /// <summary>
        /// The number of tasks executed on this worker in the past.
        /// </summary>
        public int TasksExecutedCount { get; set; }

        public async Task<int> GetReservationCountAsync()
        {
            await _reservationsLock.WaitAsync();
            try
            {
                return _reservations.Count;
            }
            finally
            {
                _reservationsLock.Release();
            }
        }

        internal async Task AddReservationAsync(WorkerStateReservation reservation)
        {
            await _reservationsLock.WaitAsync();
            try
            {
                _reservations.Add(reservation);
            }
            finally
            {
                _reservationsLock.Release();
            }
        }

        internal async Task RemoveReservationByCoreAsync(IWorkerCore core)
        {
            await _reservationsLock.WaitAsync();
            try
            {
                _reservations.RemoveAll(x => x.Core == core);
            }
            finally
            {
                _reservationsLock.Release();
            }
        }
    }
}
