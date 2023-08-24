namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Redpoint.Collections;
    using Redpoint.Concurrency;

    public class MultipleSourceWorkerCoreRequestFulfiller<TWorkerCore> : IAsyncDisposable where TWorkerCore : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly WorkerCoreRequestCollection<TWorkerCore> _requestCollection;
        private readonly WorkerCoreProviderCollection<TWorkerCore> _providerCollection;
        private readonly bool _fulfillsLocalRequests;
        private readonly CancellationTokenSource _disposedCts;
        private readonly SemaphoreSlim _processRequestsSemaphore;
        private readonly Dictionary<IWorkerCoreProvider<TWorkerCore>, WorkerCoreObtainmentState> _currentProviders;
        private readonly MutexSlim _currentProvidersLock;
        private readonly Task _backgroundTask;

        private class WorkerCoreObtainmentState
        {
            private readonly ILogger _logger;
            private readonly CancellationToken _disposedCancellationToken;
            private bool _isObtainingCore;
            private CancellationTokenSource _obtainmentCancellationTokenSource;
            private Task? _obtainmentBackgroundTask;
            private TWorkerCore? _obtainedCore;

            public bool IsObtainingCore => _isObtainingCore;

            public bool HasObtainedCore => _isObtainingCore && _obtainedCore != null;

            public WorkerCoreObtainmentState(ILogger logger, CancellationToken disposedCancellationToken)
            {
                _logger = logger;
                _disposedCancellationToken = disposedCancellationToken;
                _obtainmentCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken);
            }

            public void StartObtainingCore(Func<CancellationToken, Task> task)
            {
                if (_isObtainingCore)
                {
                    throw new InvalidOperationException("StartObtainingCore called on WorkerCoreObtainmentState that is already obtaining core.");
                }
                _isObtainingCore = true;
                var cancellationTokenSource = _obtainmentCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken);
                _obtainmentBackgroundTask = Task.Run(async () =>
                {
                    await task(cancellationTokenSource.Token);
                });
            }

            public void AcceptObtainedCore(TWorkerCore core)
            {
                if (_obtainmentBackgroundTask == null || !_isObtainingCore)
                {
                    throw new InvalidOperationException("AcceptObtainedCore called on WorkerCoreObtainmentState that was not obtaining a core.");
                }
                _obtainedCore = core;
            }

            public async Task CancelObtainingCoreAsync()
            {
                if (_obtainedCore != null)
                {
                    await _obtainedCore.DisposeAsync();
                }
                _obtainmentCancellationTokenSource.Cancel();
                _obtainmentCancellationTokenSource = new CancellationTokenSource();
                _obtainmentBackgroundTask = null;
                _obtainedCore = default;
                _isObtainingCore = false;
            }

            public void IndicateObtainmentFailed()
            {
                if (!_isObtainingCore)
                {
                    throw new InvalidOperationException("CancelObtainingCore called on WorkerCoreObtainmentState that was not obtaining a core.");
                }
                _isObtainingCore = false;
                _obtainmentCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken);
                _obtainmentBackgroundTask = null;
                _obtainedCore = default;
            }

            public TWorkerCore TakeObtainedCore()
            {
                if (_obtainedCore == null)
                {
                    throw new InvalidOperationException("TakeObtainedCore called on WorkerCoreObtainmentState that does not have core.");
                }
                var core = _obtainedCore;
                _obtainedCore = default;
                _isObtainingCore = false;
                _obtainmentCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken);
                _obtainmentBackgroundTask = null;
                return core!;
            }

            public async Task CancelObtainmentIfRunningAsync()
            {
                _obtainmentCancellationTokenSource.Cancel();
                if (_obtainmentBackgroundTask != null)
                {
                    try
                    {
                        await _obtainmentBackgroundTask;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCritical(ex, ex.Message);
                    }
                }
            }
        }

        public MultipleSourceWorkerCoreRequestFulfiller(
            ILogger logger,
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
            _currentProvidersLock = new MutexSlim();
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
            using var _ = await _currentProvidersLock.WaitAsync(token);

            if (providersInfo.AddedProvider != null)
            {
                if (!_currentProviders.ContainsKey(providersInfo.AddedProvider))
                {
                    _currentProviders.Add(providersInfo.AddedProvider, new WorkerCoreObtainmentState(_logger, _disposedCts.Token));
                }
            }
            if (providersInfo.RemovedProvider != null)
            {
                if (_currentProviders.ContainsKey(providersInfo.RemovedProvider))
                {
                    var state = _currentProviders[providersInfo.RemovedProvider];
                    await state.CancelObtainmentIfRunningAsync();
                    _currentProviders.Remove(providersInfo.RemovedProvider);
                }
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
                using (await _currentProvidersLock.WaitAsync(_disposedCts.Token))
                {
                    try
                    {
                        foreach (var provider in await _providerCollection.GetProvidersAsync())
                        {
                            _currentProviders.Add(provider, new WorkerCoreObtainmentState(_logger, _disposedCts.Token));
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

                        using (await _currentProvidersLock.WaitAsync(_disposedCts.Token))
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
                                var coreAvailableToFulfillRequest = _currentProviders.Values.FirstOrDefault(x => x.HasObtainedCore);
                                if (coreAvailableToFulfillRequest == null)
                                {
                                    // No more cores immediately available.
                                    remainingUnfulfilledRequests.Add(request);
                                }
                                else
                                {
                                    // Fulfill the request.
                                    var core = coreAvailableToFulfillRequest.TakeObtainedCore();
                                    if (core is IWorkerCoreWithLiveness coreWithLiveness &&
                                        !(await coreWithLiveness.IsAliveAsync(_disposedCts.Token)))
                                    {
                                        // This core is dead. Do not use it.
                                        await core.DisposeAsync();
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
                                    provider.Value.StartObtainingCore(async cancellationToken =>
                                    {
                                        var didAssignObtainedCore = false;
                                        try
                                        {
                                            var obtainedCore = await provider.Key.RequestCoreAsync(cancellationToken);
                                            try
                                            {
                                                using (await _currentProvidersLock.WaitAsync(cancellationToken))
                                                {
                                                    provider.Value.AcceptObtainedCore(obtainedCore);
                                                    didAssignObtainedCore = true;
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
                                            if (!didAssignObtainedCore && provider.Value.IsObtainingCore)
                                            {
                                                using (await _currentProvidersLock.WaitAsync(CancellationToken.None))
                                                {
                                                    provider.Value.IndicateObtainmentFailed();
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
                                    await provider.Value.CancelObtainingCoreAsync();
                                }
                            }
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
