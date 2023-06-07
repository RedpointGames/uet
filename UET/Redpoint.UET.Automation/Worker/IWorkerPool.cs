namespace Redpoint.UET.Automation.Worker
{
    using System.Threading.Tasks;

    public interface IWorkerPool : IAsyncDisposable
    {
        /// <summary>
        /// Tell the worker pool that a worker is being reserved for work to be done. Worker pools use this information to know when the pool is at capacity and can use this to scale up more workers.
        /// </summary>
        /// <param name="worker">The worker being reserved. You can get a worker from the <see cref="OnWorkerStarted"/> event.</param>
        /// <returns>The object to dispose when the worker is no longer being used.</returns>
        Task<IAsyncDisposable> ReserveAsync(IWorker worker);

        /// <summary>
        /// Tell the worker pool that a worker is finished, and no more work is expected to be scheduled. This will make the worker pool shutdown the worker and it will not be replaced with a new instance.
        /// </summary>
        /// <param name="worker">The worker to finish.</param>
        void FinishedWithWorker(IWorker worker);

        /// <summary>
        /// Tell the worker pool that a descriptor is finished. All workers associated with the descriptor should be shutdown, and no new workers created.
        /// </summary>
        /// <param name="descriptor">The descriptor to finish.</param>
        void FinishedWithDescriptor(DesiredWorkerDescriptor descriptor);

        /// <summary>
        /// Tell the worker pool that a worker should be killed. This should be used when the caller knows the worker is permanently in a bad state (though it hasn't exited), and wants the worker pool to kill it and launch a replacement.
        /// </summary>
        /// <param name="worker">The worker to kill.</param>
        void KillWorker(IWorker worker);
    }
}
