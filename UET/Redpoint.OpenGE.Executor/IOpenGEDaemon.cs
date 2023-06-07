namespace Redpoint.OpenGE.Executor
{
    using Redpoint.ApplicationLifecycle;

    public interface IOpenGEDaemon : IApplicationLifecycle
    {
        string GetConnectionString();
    }
}
