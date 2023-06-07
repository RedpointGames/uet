namespace Redpoint.ApplicationLifecycle
{
    public interface IApplicationLifecycle
    {
        Task StartAsync(CancellationToken shutdownCancellationToken);

        Task StopAsync();
    }
}