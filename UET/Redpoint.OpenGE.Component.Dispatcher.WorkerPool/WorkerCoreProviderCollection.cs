namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class WorkerCoreProviderCollection<TWorkerCore> where TWorkerCore : IAsyncDisposable
    {
        private readonly Dictionary<string, IWorkerCoreProvider<TWorkerCore>> _providers;
        private readonly MutexSlim _providerLock;
        private readonly AsyncEvent<WorkerCoreProviderCollectionChanged<TWorkerCore>> _onProvidersChanged;

        public WorkerCoreProviderCollection()
        {
            _providers = new Dictionary<string, IWorkerCoreProvider<TWorkerCore>>();
            _providerLock = new MutexSlim();
            _onProvidersChanged = new AsyncEvent<WorkerCoreProviderCollectionChanged<TWorkerCore>>();
        }

        public IAsyncEvent<WorkerCoreProviderCollectionChanged<TWorkerCore>> OnProvidersChanged => _onProvidersChanged;

        public async Task<IReadOnlyList<IWorkerCoreProvider<TWorkerCore>>> GetProvidersAsync()
        {
            using var _ = await _providerLock.WaitAsync(CancellationToken.None);
            return new List<IWorkerCoreProvider<TWorkerCore>>(_providers.Values);
        }

        public async Task<bool> HasAsync(string providerId)
        {
            using var _ = await _providerLock.WaitAsync(CancellationToken.None);
            return _providers.ContainsKey(providerId);
        }

        public async Task AddAsync(IWorkerCoreProvider<TWorkerCore> provider)
        {
            using var _ = await _providerLock.WaitAsync(CancellationToken.None);

            if (_providers.ContainsKey(provider.Id))
            {
                return;
            }
            _providers.Add(provider.Id, provider);
            try
            {
                await _onProvidersChanged.BroadcastAsync(new WorkerCoreProviderCollectionChanged<TWorkerCore>
                {
                    CurrentProviders = new List<IWorkerCoreProvider<TWorkerCore>>(_providers.Values),
                    AddedProvider = provider,
                    RemovedProvider = null,
                }, CancellationToken.None);
            }
            catch
            {
            }
        }

        public async Task RemoveAsync(IWorkerCoreProvider<TWorkerCore> provider)
        {
            using var _ = await _providerLock.WaitAsync(CancellationToken.None);

            if (_providers.ContainsKey(provider.Id))
            {
                _providers.Remove(provider.Id);
                try
                {
                    await _onProvidersChanged.BroadcastAsync(new WorkerCoreProviderCollectionChanged<TWorkerCore>
                    {
                        CurrentProviders = new List<IWorkerCoreProvider<TWorkerCore>>(_providers.Values),
                        AddedProvider = null,
                        RemovedProvider = provider,
                    }, CancellationToken.None);
                }
                catch
                {
                }
            }
        }
    }
}
