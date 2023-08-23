namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    using System.Threading;
    using System.Threading.Tasks;

    internal class TestCoreProvider : IWorkerCoreProvider<NullWorkerCore>
    {
        public SemaphoreSlim ProvideCore = new SemaphoreSlim(0);

        public async Task<NullWorkerCore> RequestCoreAsync(CancellationToken cancellationToken)
        {
            await ProvideCore.WaitAsync(cancellationToken);
            return new NullWorkerCore();
        }
    }
}
