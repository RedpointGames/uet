namespace Redpoint.OpenGE.Agent
{
    using Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor;

    public interface IOpenGEAgent : IPreprocessorCacheAccessor
    {
        Task StartAsync();
        string DispatcherConnectionString { get; }
        Task StopAsync();
    }
}