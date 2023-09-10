namespace Redpoint.Tasks
{
    internal class DefaultTaskScheduler : ITaskScheduler
    {
        public ITaskSchedulerScope CreateSchedulerScope(
            string scopeName,
            CancellationToken cancellationToken)
        {
            return new DefaultTaskSchedulerScope(
                scopeName,
                cancellationToken);
        }
    }
}