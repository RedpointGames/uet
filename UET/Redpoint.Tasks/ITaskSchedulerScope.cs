namespace Redpoint.Tasks
{
    public interface ITaskSchedulerScope : IAsyncDisposable, IDisposable
    {
        Task RunAsync(
            string taskName,
            CancellationToken taskCancellationToken,
            Func<CancellationToken, Task> backgroundTask);

        string[] GetCurrentlyExecutingTasks();
    }
}