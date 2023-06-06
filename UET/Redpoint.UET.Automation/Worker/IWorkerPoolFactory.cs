namespace Redpoint.UET.Automation.Worker
{
    using System.Threading.Tasks;

    public interface IWorkerPoolFactory
    {
        /// <summary>
        /// Creates a new worker pool for the desired worker descriptors and starts it.
        /// </summary>
        /// <param name="workerDescriptors">The workers that you want started. A worker pool can share workers of different types and devices. You can get the descriptor later when workers are started by checking <see cref="IWorker.Descriptor"/>.</param>
        /// <param name="onWorkerStarted">Called when a worker is started by the pool.</param>
        /// <param name="onWorkedExited">Called when a worker exits (either normally or due to a crash).</param>
        /// <param name="onWorkerPoolFailure">Called when the worker pool fails and can not launch the desired workers.</param>
        /// <param name="cancellationToken">The cancellation token to cancel when all workers should be shutdown. You should await the <see cref="IAsyncDisposable.DisposeAsync"/> to know when all workers have been cleanly shutdown after cancellation.</param>
        /// <returns>The task to await for startup.</returns>
        Task<IWorkerPool> CreateAndStartAsync(
            IEnumerable<DesiredWorkerDescriptor> workerDescriptors,
            OnWorkerStarted onWorkerStarted,
            OnWorkerExited onWorkedExited,
            OnWorkerPoolFailure onWorkerPoolFailure,
            CancellationToken cancellationToken);
    }
}
