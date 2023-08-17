namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.OpenGE.Protocol;

    public class WorkerAddRequest
    {
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

        /// <summary>
        /// The client for the remote worker that we will communicate on.
        /// </summary>
        public required TaskApi.TaskApiClient Client { get; init; }
    }
}
