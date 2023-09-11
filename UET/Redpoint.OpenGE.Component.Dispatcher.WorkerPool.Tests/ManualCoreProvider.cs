namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class ManualCoreProvider : IWorkerCoreProvider<IWorkerCore>
    {
        private Concurrency.Semaphore _provideCore = new Concurrency.Semaphore(0);

        public ManualCoreProvider()
        {
            Id = Guid.NewGuid().ToString();
        }

        public string Id { get; }

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
            await _provideCore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new ManualWorkerCore();
        }

        private sealed class ManualWorkerCore : IWorkerCore
        {
            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
