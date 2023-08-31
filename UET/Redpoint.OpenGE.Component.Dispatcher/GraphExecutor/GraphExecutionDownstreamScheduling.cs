namespace Redpoint.OpenGE.Component.Dispatcher.GraphExecutor
{
    internal enum GraphExecutionDownstreamScheduling
    {
        /// <summary>
        /// Downstream tasks need to be scheduled and executed by the main graph execution loop.
        /// </summary>
        ScheduleByGraphExecution,

        /// <summary>
        /// The downstream tasks are about to be immediately executed, without going via the main loop.
        /// </summary>
        ImmediatelyScheduledDueToFastExecution,
    }
}
