namespace Redpoint.OpenGE.Executor
{
    using Redpoint.ApplicationLifecycle;

    public interface IOpenGEDaemon : IApplicationLifecycle
    {
        bool IsDaemonRunning { get; }

        string GetConnectionString();
    }
}
