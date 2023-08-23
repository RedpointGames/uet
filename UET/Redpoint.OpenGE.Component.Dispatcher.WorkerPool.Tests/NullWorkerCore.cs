namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    internal class NullWorkerCore : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}