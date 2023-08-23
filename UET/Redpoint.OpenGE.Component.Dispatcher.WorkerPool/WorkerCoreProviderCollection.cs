namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class WorkerCoreProviderCollection<TWorkerCore> where TWorkerCore : IAsyncDisposable
    {
        private readonly List<IWorkerCoreProvider<TWorkerCore>> _providers;
        private readonly SemaphoreSlim _providerLock;
        private readonly AsyncEvent<WorkerCoreProviderCollectionChanged<TWorkerCore>> _onProvidersChanged;

        public WorkerCoreProviderCollection()
        {
            _providers = new List<IWorkerCoreProvider<TWorkerCore>>();
            _providerLock = new SemaphoreSlim(1);
            _onProvidersChanged = new AsyncEvent<WorkerCoreProviderCollectionChanged<TWorkerCore>>();
        }

        public IAsyncEvent<WorkerCoreProviderCollectionChanged<TWorkerCore>> OnProvidersChanged => _onProvidersChanged;

        public async Task AddAsync(IWorkerCoreProvider<TWorkerCore> provider)
        {
            await _providerLock.WaitAsync();
            try
            {
                _providers.Add(provider);
                try
                {
                    await _onProvidersChanged.BroadcastAsync(new WorkerCoreProviderCollectionChanged<TWorkerCore>
                    {
                        CurrentProviders = new List<IWorkerCoreProvider<TWorkerCore>>(_providers),
                        AddedProvider = provider,
                        RemovedProvider = null,
                    }, CancellationToken.None);
                }
                catch
                {
                }
            }
            finally
            {
                _providerLock.Release();
            }
        }

        public async Task RemoveAsync(IWorkerCoreProvider<TWorkerCore> provider)
        {
            await _providerLock.WaitAsync();
            try
            {
                if (_providers.Contains(provider))
                {
                    _providers.Remove(provider);
                    try
                    {
                        await _onProvidersChanged.BroadcastAsync(new WorkerCoreProviderCollectionChanged<TWorkerCore>
                        {
                            CurrentProviders = new List<IWorkerCoreProvider<TWorkerCore>>(_providers),
                            AddedProvider = null,
                            RemovedProvider = provider,
                        }, CancellationToken.None);
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                _providerLock.Release();
            }
        }
    }
}
