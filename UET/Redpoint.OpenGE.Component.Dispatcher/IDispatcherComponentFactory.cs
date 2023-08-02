namespace Redpoint.OpenGE.Component.Dispatcher
{
    public interface IDispatcherComponentFactory
    {
        IDispatcherComponent Create(string? pipeName);
    }
}
