namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    using System.Threading;
    using System.Threading.Tasks;

    internal class ManualCoreProvider : IWorkerCoreProvider<IWorkerCore>
    {
        private SemaphoreSlim _provideCore = new SemaphoreSlim(0);

        public ManualCoreProvider()
        {
        }

        public void ReleaseCore()
        {
            _provideCore.Release(1);
        }

        public void ReleaseCores(int count)
        {
            _provideCore.Release(count);
        }

        public async Task<IWorkerCore> RequestCoreAsync(CancellationToken cancellationToken)
        {
            await _provideCore.WaitAsync(cancellationToken);
            return new ManualWorkerCore();
        }

        private class ManualWorkerCore : IWorkerCore
        {
            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
