namespace Redpoint.Tasks
{
    public interface ITaskSchedulerScope : IAsyncDisposable, IDisposable
    {
        Task RunAsync(
            string taskName,
            Func<CancellationToken, Task> backgroundTask,
            CancellationToken taskCancellationToken);

        string[] GetCurrentlyExecutingTasks();
    }
}