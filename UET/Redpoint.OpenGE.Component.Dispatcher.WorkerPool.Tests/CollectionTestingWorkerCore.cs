namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    using System.Threading.Tasks;

    internal sealed class CollectionTestingWorkerCore : IWorkerCore
    {
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
