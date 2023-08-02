namespace Redpoint.OpenGE.Component.Dispatcher
{
    using Redpoint.ApplicationLifecycle;

    public interface IDispatcherComponent : IApplicationLifecycle
    {
        bool IsDaemonRunning { get; }

        string GetConnectionString();
    }
}
