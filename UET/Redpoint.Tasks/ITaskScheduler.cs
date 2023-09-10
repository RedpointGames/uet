namespace Redpoint.Tasks
{
    public interface ITaskScheduler
    {
        ITaskSchedulerScope CreateSchedulerScope(
            string scopeName,
            CancellationToken scopeCancellationToken);
    }
}