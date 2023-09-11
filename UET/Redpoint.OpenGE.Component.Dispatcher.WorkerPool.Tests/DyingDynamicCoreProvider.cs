namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool.Tests
{
    using System.Threading;
    using System.Threading.Tasks;

    internal class DyingDynamicCoreProvider : IWorkerCoreProvider<IWorkerCore>
    {
        private Concurrency.Semaphore _provideCore = new Concurrency.Semaphore(0);

        public DyingDynamicCoreProvider(int coresAvailable)
        {
            _provideCore.Release(coresAvailable);
            Id = Guid.NewGuid().ToString();
        }

        public string Id { get; }

        public async Task<IWorkerCore> RequestCoreAsync(CancellationToken cancellationToken)
        {
            await _provideCore.WaitAsync(cancellationToken);
            return new DyingDynamicWorkerCore(this);
        }

        private class DyingDynamicWorkerCore : IWorkerCoreWithLiveness
        {
            private readonly DyingDynamicCoreProvider _provider;
            private readonly bool _dead;

            public DyingDynamicWorkerCore(DyingDynamicCoreProvider provider)
            {
                _provider = provider;
                _dead = Random.Shared.Next(0, 2) == 0;
            }

            public ValueTask DisposeAsync()
            {
                _provider._provideCore.Release(1);
                return ValueTask.CompletedTask;
            }

            public ValueTask<bool> IsAliveAsync(CancellationToken cancellationToken)
            {
                return ValueTask.FromResult(!_dead);
            }
        }
    }
}
