namespace Redpoint.OpenGE.Component.Dispatcher.GraphExecutor
{
    internal enum GraphTaskStatus
    {
        /// <summary>
        /// This task is not queued for processing yet, because it is still waiting on
        /// it's dependencies to run first.
        /// </summary>
        Pending,

        /// <summary>
        /// This task has been put into the queue for execution, or is currently executing.
        /// </summary>
        Scheduled,

        /// <summary>
        /// This task has completed successfully.
        /// </summary>
        CompletedSuccessfully,

        /// <summary>
        /// This task has completed in a non-successful state.
        /// </summary>
        CompletedUnsuccessfully,

        /// <summary>
        /// This task will never be scheduled, as one of it's upstream dependencies
        /// has failed so it can't run.
        /// </summary>
        CancelledDueToUpstreamFailures,
    }
}
