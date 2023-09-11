namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class DynamicCoreProvider : IWorkerCoreProvider<IWorkerCore>
    {
        private Concurrency.Semaphore _provideCore = new Concurrency.Semaphore(0);

        public DynamicCoreProvider(int coresAvailable)
        {
            _provideCore.Release(coresAvailable);
            Id = Guid.NewGuid().ToString();
        }

        public string Id { get; }

        public async Task<IWorkerCore> RequestCoreAsync(CancellationToken cancellationToken)
        {
            await _provideCore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new DynamicWorkerCore(this);
        }

        private sealed class DynamicWorkerCore : IWorkerCore
        {
            private readonly DynamicCoreProvider _provider;

            public DynamicWorkerCore(DynamicCoreProvider provider)
            {
                _provider = provider;
            }

            public ValueTask DisposeAsync()
            {
                _provider._provideCore.Release(1);
                return ValueTask.CompletedTask;
            }
        }
    }
}
