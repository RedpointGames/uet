namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Concurrent;

    internal class RemoteWorkerState
    {
        private readonly List<RemoteWorkerStateReservation> _reservations = new List<RemoteWorkerStateReservation>();
        private readonly SemaphoreSlim _reservationsLock = new SemaphoreSlim(1);

        /// <summary>
        /// The client for the remote worker that we will communicate on.
        /// </summary>
        public required TaskApi.TaskApiClient Client { get; init; }

        /// <summary>
        /// The pending reservation process, if there is one.
        /// </summary>
        public RemoteWorkerStatePendingReservation? PendingReservation { get; set; }

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

        internal async Task AddReservationAsync(RemoteWorkerStateReservation reservation)
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
