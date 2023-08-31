namespace Redpoint.OpenGE.Component.Dispatcher
{
    using Redpoint.ApplicationLifecycle;
    using Redpoint.OpenGE.Protocol;

    public interface IDispatcherComponent : IApplicationLifecycle
    {
        JobApi.JobApiBase JobApi { get; }

        bool IsDaemonRunning { get; }

        string GetConnectionString();
    }
}
