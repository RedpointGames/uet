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
        /// This task has been put into the task queue for execution, but the background task hasn't started executing yet.
        /// </summary>
        Scheduled,

        /// <summary>
        /// This task has started execution on the .NET TPL.
        /// </summary>
        Starting,

        /// <summary>
        /// This task is trying to obtain a local core for fast local execution.
        /// </summary>
        WaitingForFastLocalCore,

        /// <summary>
        /// This task is computing the task descriptor.
        /// </summary>
        ComputingTaskDescriptor,

        /// <summary>
        /// This task is trying to obtain a core suitable for the task.
        /// </summary>
        WaitingForCore,

        /// <summary>
        /// This task is executing the task descriptor.
        /// </summary>
        ExecutingTaskDescriptor,

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
