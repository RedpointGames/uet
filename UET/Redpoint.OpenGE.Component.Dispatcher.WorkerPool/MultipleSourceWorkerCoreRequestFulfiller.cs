namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Redpoint.Collections;

    internal class MultipleSourceWorkerCoreRequestFulfiller<TWorkerCore> : IAsyncDisposable where TWorkerCore : IAsyncDisposable
    {
        private readonly ILogger<MultipleSourceWorkerCoreRequestFulfiller<TWorkerCore>> _logger;
        private readonly WorkerCoreRequestCollection<TWorkerCore> _requestCollection;
        private readonly WorkerCoreProviderCollection<TWorkerCore> _providerCollection;
        private readonly bool _fulfillsLocalRequests;
        private readonly CancellationTokenSource _disposedCts;
        private readonly SemaphoreSlim _processRequestsSemaphore;
        private readonly Dictionary<IWorkerCoreProvider<TWorkerCore>, WorkerCoreObtainmentState> _currentProviders;
        private readonly SemaphoreSlim _currentProvidersLock;
        private readonly Task _backgroundTask;

        private class WorkerCoreObtainmentState
        {
            public bool IsObtainingCore { get; set; }

            public CancellationTokenSource ObtainmentCancellationTokenSource { get; set; } = new CancellationTokenSource();

            public Task? ObtainmentBackgroundTask { get; set; }

            public TWorkerCore? ObtainedCore { get; set; }
        }

        public MultipleSourceWorkerCoreRequestFulfiller(
            ILogger<MultipleSourceWorkerCoreRequestFulfiller<TWorkerCore>> logger,
            WorkerCoreRequestCollection<TWorkerCore> requestCollection,
            WorkerCoreProviderCollection<TWorkerCore> providerCollection,
            bool canFulfillLocalRequests)
        {
            _logger = logger;
            _requestCollection = requestCollection;
            _providerCollection = providerCollection;
            _fulfillsLocalRequests = canFulfillLocalRequests;
            _disposedCts = new CancellationTokenSource();
            _processRequestsSemaphore = new SemaphoreSlim(1);
            _currentProviders = new Dictionary<IWorkerCoreProvider<TWorkerCore>, WorkerCoreObtainmentState>();
            _currentProvidersLock = new SemaphoreSlim(1);
            _backgroundTask = Task.Run(RunAsync);
        }

        public async ValueTask DisposeAsync()
        {
            _disposedCts.Cancel();
            await _requestCollection.OnRequestsChanged.RemoveAsync(OnNotifiedRequestsChanged);
            try
            {
                await _backgroundTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        private Task OnNotifiedRequestsChanged(WorkerCoreRequestStatistics statistics, CancellationToken token)
        {
            _processRequestsSemaphore.Release();
            return Task.CompletedTask;
        }

        private async Task OnNotifiedProvidersChanged(WorkerCoreProviderCollectionChanged<TWorkerCore> providersInfo, CancellationToken token)
        {
            await _currentProvidersLock.WaitAsync(token);
            try
            {
                if (providersInfo.AddedProvider != null)
                {
                    if (!_currentProviders.ContainsKey(providersInfo.AddedProvider))
                    {
                        _currentProviders.Add(providersInfo.AddedProvider, new WorkerCoreObtainmentState());
                    }
                }
                if (providersInfo.RemovedProvider != null)
                {
                    if (_currentProviders.ContainsKey(providersInfo.RemovedProvider))
                    {
                        var state = _currentProviders[providersInfo.RemovedProvider];
                        state.ObtainmentCancellationTokenSource.Cancel();
                        if (state.ObtainmentBackgroundTask != null)
                        {
                            try
                            {
                                await state.ObtainmentBackgroundTask;
                            }
                            catch (OperationCanceledException)
                            {
                            }
                            catch (Exception ex)
                            {
                                _logger.LogCritical(ex, ex.Message);
                            }
                        }
                        _currentProviders.Remove(providersInfo.RemovedProvider);
                    }
                }
            }
            finally
            {
                _currentProvidersLock.Release();
            }
            _processRequestsSemaphore.Release();
        }

        private async Task RunAsync()
        {
            // Set up our events.
            while (true)
            {
                try
                {
                    await _requestCollection.OnRequestsChanged.AddAsync(OnNotifiedRequestsChanged);
                    break;
                }
                catch (OperationCanceledException) when (_disposedCts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, ex.Message);
                    await Task.Delay(1000);
                }
            }
            while (true)
            {
                try
                {
                    await _providerCollection.OnProvidersChanged.AddAsync(OnNotifiedProvidersChanged);
                    break;
                }
                catch (OperationCanceledException) when (_disposedCts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, ex.Message);
                    await Task.Delay(1000);
                }
            }

            // Get the initial providers.
            while (true)
            {
                await _currentProvidersLock.WaitAsync(_disposedCts.Token);
                try
                {
                    foreach (var provider in await _providerCollection.GetProvidersAsync())
                    {
                        _currentProviders.Add(provider, new WorkerCoreObtainmentState());
                    }
                    break;
                }
                catch (OperationCanceledException) when (_disposedCts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, ex.Message);
                    await Task.Delay(1000);
                }
                finally
                {
                    _currentProvidersLock.Release();
                }
            }

            // Process requests.
            while (true)
            {
                try
                {
                    while (!_disposedCts.IsCancellationRequested)
                    {
                        await _processRequestsSemaphore.WaitAsync(_disposedCts.Token);

                        await _currentProvidersLock.WaitAsync(_disposedCts.Token);
                        try
                        {
                            // Step 1: Get the list of requests we haven't fulfilled yet.
                            var initiallyUnfulfilledRequests = _fulfillsLocalRequests
                                ? await _requestCollection.GetUnfulfilledLocalRequestsAsync(_disposedCts.Token)
                                : await _requestCollection.GetUnfulfilledRemotableRequestsAsync(_disposedCts.Token);

                            // Step 2: Fulfill any unfulfilled requests that we can immediately fulfill
                            // now (because our obtainment tasks have obtained cores).
                            var remainingUnfulfilledRequests = new List<IWorkerCoreRequest<TWorkerCore>>();
                            foreach (var request in initiallyUnfulfilledRequests)
                            {
                            retryCore:
                                var coreAvailableToFulfillRequest = _currentProviders.Values.FirstOrDefault(x => x.ObtainedCore != null);
                                if (coreAvailableToFulfillRequest == null)
                                {
                                    // No more cores immediately available.
                                    remainingUnfulfilledRequests.Add(request);
                                }
                                else
                                {
                                    // Fulfill the request.
                                    var core = coreAvailableToFulfillRequest.ObtainedCore!;
                                    coreAvailableToFulfillRequest.ObtainedCore = default;
                                    coreAvailableToFulfillRequest.IsObtainingCore = false;
                                    coreAvailableToFulfillRequest.ObtainmentCancellationTokenSource = new CancellationTokenSource();
                                    coreAvailableToFulfillRequest.ObtainmentBackgroundTask = null;
                                    if (core is IWorkerCoreWithLiveness coreWithLiveness &&
                                        !(await coreWithLiveness.IsAliveAsync(_disposedCts.Token)))
                                    {
                                        // This core is dead. Do not use it.
                                        goto retryCore;
                                    }
                                    else
                                    {
                                        // Assign the core.
                                        await request.FulfillRequestAsync(core);
                                    }
                                }
                            }

                            // Step 3: Calculate how many cores we will need based on pending requests.
                            var desiredCores = remainingUnfulfilledRequests.Count;

                            // Step 4: Figure out how many cores we should be trying to obtain in this moment.
                            // We want more cores than the actual number of desired cores, so that if we only
                            // need one core, we'll ask for a core from multiple providers (so that we don't
                            // get blocked by the one random remote worker we picked being busy).
                            var obtainmentTargetCores = desiredCores == 0 ? 0 : (desiredCores + 3);

                            // Step 5: Calculate how many cores we're in the process of trying to fulfill
                            // via obtainment tasks.
                            var obtainmentCurrentCores = _currentProviders.Values.Count(x => x.IsObtainingCore);

                            // Step 6: Figure out the difference between the number of cores we should be
                            // obtaining, and the number of cores we're currently obtaining.
                            var differenceCores = obtainmentTargetCores - obtainmentCurrentCores;

                            // Step 7: Either start obtaining more cores, or cancel obtaining cores that we no
                            // longer need.
                            if (differenceCores > 0)
                            {
                                // Start background tasks to obtain more cores.
                                var idleProviders = _currentProviders.Where(x => !x.Value.IsObtainingCore).ToList();
                                idleProviders.Shuffle();
                                for (int i = 0; i < differenceCores && i < idleProviders.Count; i++)
                                {
                                    var provider = idleProviders[i];
                                    provider.Value.IsObtainingCore = true;
                                    var cts = provider.Value.ObtainmentCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_disposedCts.Token);
                                    provider.Value.ObtainmentBackgroundTask = Task.Run(async () =>
                                    {
                                        var didAssignObtainedCore = false;
                                        try
                                        {
                                            var obtainedCore = await provider.Key.RequestCoreAsync(cts.Token);
                                            try
                                            {
                                                await _currentProvidersLock.WaitAsync(cts.Token);
                                                try
                                                {
                                                    if (provider.Value.ObtainmentBackgroundTask != null &&
                                                        provider.Value.IsObtainingCore)
                                                    {
                                                        provider.Value.ObtainedCore = obtainedCore;
                                                        didAssignObtainedCore = true;
                                                    }
                                                }
                                                finally
                                                {
                                                    _currentProvidersLock.Release();
                                                }
                                            }
                                            finally
                                            {
                                                if (!didAssignObtainedCore)
                                                {
                                                    await obtainedCore.DisposeAsync();
                                                }
                                            }
                                        }
                                        finally
                                        {
                                            if (!didAssignObtainedCore)
                                            {
                                                await _currentProvidersLock.WaitAsync(CancellationToken.None);
                                                try
                                                {
                                                    provider.Value.IsObtainingCore = false;
                                                }
                                                finally
                                                {
                                                    _currentProvidersLock.Release();
                                                }
                                            }
                                        }
                                        if (didAssignObtainedCore)
                                        {
                                            _processRequestsSemaphore.Release();
                                        }
                                    });
                                }
                            }
                            else if (differenceCores < 0)
                            {
                                // Kill off obtainment tasks and release cores that we no longer need.
                                var workingProviders = _currentProviders.Where(x => x.Value.IsObtainingCore).ToList();
                                workingProviders.Shuffle();
                                for (int i = 0; i < -differenceCores && i < workingProviders.Count; i++)
                                {
                                    var provider = workingProviders[i];
                                    if (provider.Value.ObtainedCore != null)
                                    {
                                        await provider.Value.ObtainedCore.DisposeAsync();
                                    }
                                    provider.Value.ObtainmentCancellationTokenSource.Cancel();
                                    provider.Value.ObtainmentCancellationTokenSource = new CancellationTokenSource();
                                    provider.Value.ObtainmentBackgroundTask = null;
                                    provider.Value.ObtainedCore = default;
                                }
                            }
                        }
                        finally
                        {
                            _currentProvidersLock.Release();
                        }
                    }

                    return;
                }
                catch (OperationCanceledException) when (_disposedCts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, ex.Message);
                    await Task.Delay(1000);
                }
            }
        }
    }
}
