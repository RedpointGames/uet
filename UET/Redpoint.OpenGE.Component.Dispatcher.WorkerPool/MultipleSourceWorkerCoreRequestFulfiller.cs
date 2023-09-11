namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Redpoint.Collections;
    using Redpoint.Concurrency;
    using Redpoint.Tasks;

    public class MultipleSourceWorkerCoreRequestFulfiller<TWorkerCore> : IAsyncDisposable, IWorkerPoolTracerAssignable where TWorkerCore : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly ITaskSchedulerScope _taskSchedulerScope;
        private readonly WorkerCoreRequestCollection<TWorkerCore> _requestCollection;
        private readonly WorkerCoreProviderCollection<TWorkerCore> _providerCollection;
        private readonly bool _fulfillsLocalRequests;
        private readonly Semaphore _processRequestsSemaphore;
        private readonly Dictionary<IWorkerCoreProvider<TWorkerCore>, WorkerCoreObtainmentState> _currentProviders;
        private readonly Mutex _currentProvidersLock;
        private readonly Task _backgroundTask;
        private WorkerPoolTracer? _tracer;

        public MultipleSourceWorkerCoreRequestFulfillerStatistics<TWorkerCore> GetStatistics()
        {
            using (_currentProvidersLock.Wait(CancellationToken.None))
            {
                return new MultipleSourceWorkerCoreRequestFulfillerStatistics<TWorkerCore>
                {
                    Providers = _currentProviders.ToDictionary(
                        k => k.Key,
                        v => new MultipleSourceWorkerCoreRequestFulfillerStatisticsForCore<TWorkerCore>
                        {
                            UniqueId = v.Value._uniqueId,
                            IsObtainingCore = v.Value._isObtainingCore,
                            ObtainedCore = v.Value._obtainedCore,
                        }),
                };
            }
        }

        private sealed class WorkerCoreObtainmentState : IDisposable
        {
            private readonly MultipleSourceWorkerCoreRequestFulfiller<TWorkerCore> _owner;
            private readonly ILogger _logger;
            internal readonly string _uniqueId;
            internal bool _isObtainingCore;
            private CancellationTokenSource _obtainmentCancellationTokenSource;
            private Task? _obtainmentBackgroundTask;
            internal TWorkerCore? _obtainedCore;

            public bool IsObtainingCore => _isObtainingCore;

            public bool HasObtainedCore => _isObtainingCore && _obtainedCore != null;

            public WorkerCoreObtainmentState(
                MultipleSourceWorkerCoreRequestFulfiller<TWorkerCore> owner,
                ILogger logger,
                string uniqueId)
            {
                _owner = owner;
                _logger = logger;
                _uniqueId = uniqueId;
                _obtainmentCancellationTokenSource = new CancellationTokenSource();
            }

            public override string ToString()
            {
                return _uniqueId;
            }

            public void StartObtainingCore(Func<CancellationToken, Task> task)
            {
                if (_isObtainingCore)
                {
                    throw new InvalidOperationException("StartObtainingCore called on WorkerCoreObtainmentState that is already obtaining core.");
                }
                _isObtainingCore = true;
                var cancellationTokenSource = _obtainmentCancellationTokenSource = new CancellationTokenSource();
                _obtainmentBackgroundTask = _owner._taskSchedulerScope.RunAsync("StartObtainingCore", async (cancellationToken) =>
                {
                    await task(cancellationToken).ConfigureAwait(false);
                }, cancellationTokenSource.Token);
            }

            public void AcceptObtainedCore(TWorkerCore core)
            {
                if (_obtainmentBackgroundTask == null || !_isObtainingCore)
                {
                    throw new InvalidOperationException("AcceptObtainedCore called on WorkerCoreObtainmentState that was not obtaining a core.");
                }
                _obtainedCore = core;
                _owner._tracer?.AddTracingMessage($"AcceptObtainedCore: {core}");
            }

            public async Task CancelObtainingCoreAsync()
            {
                if (_obtainedCore != null)
                {
                    _owner._tracer?.AddTracingMessage($"CancelObtainingCoreAsync: {_obtainedCore}");
                    await _obtainedCore.DisposeAsync().ConfigureAwait(false);
                }
                _obtainmentCancellationTokenSource.Cancel();
                _obtainmentCancellationTokenSource = new CancellationTokenSource();
                _obtainmentBackgroundTask = null;
                _obtainedCore = default;
                _isObtainingCore = false;
                _owner._tracer?.AddTracingMessage($"CancelObtainingCoreAsync: [cancelled]");
            }

            public void IndicateObtainmentFailed()
            {
                if (!_isObtainingCore)
                {
                    throw new InvalidOperationException("CancelObtainingCore called on WorkerCoreObtainmentState that was not obtaining a core.");
                }
                _isObtainingCore = false;
                _obtainmentCancellationTokenSource = new CancellationTokenSource();
                _obtainmentBackgroundTask = null;
                _obtainedCore = default;
                _owner._tracer?.AddTracingMessage($"IndicateObtainmentFailed: [obtainment failed]");
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
                _obtainmentCancellationTokenSource = new CancellationTokenSource();
                _obtainmentBackgroundTask = null;
                _owner._tracer?.AddTracingMessage($"TakeObtainedCore: {core}");
                return core!;
            }

            public async Task CancelObtainmentIfRunningAsync()
            {
                _owner._tracer?.AddTracingMessage($"CancelObtainmentIfRunningAsync");
                _obtainmentCancellationTokenSource.Cancel();
                if (_obtainmentBackgroundTask != null)
                {
                    try
                    {
                        await _obtainmentBackgroundTask.ConfigureAwait(false);
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

            public void Dispose()
            {
                _obtainmentCancellationTokenSource.Dispose();
            }
        }

        public MultipleSourceWorkerCoreRequestFulfiller(
            ILogger logger,
            ITaskScheduler taskScheduler,
            WorkerCoreRequestCollection<TWorkerCore> requestCollection,
            WorkerCoreProviderCollection<TWorkerCore> providerCollection,
            bool canFulfillLocalRequests)
        {
            if (taskScheduler == null) throw new ArgumentNullException(nameof(taskScheduler));

            _logger = logger;
            _taskSchedulerScope = taskScheduler.CreateSchedulerScope("MultipleSourceWorkerCoreRequestFulfiller", CancellationToken.None);
            _requestCollection = requestCollection;
            _providerCollection = providerCollection;
            _fulfillsLocalRequests = canFulfillLocalRequests;
            _processRequestsSemaphore = new Semaphore(1);
            _currentProviders = new Dictionary<IWorkerCoreProvider<TWorkerCore>, WorkerCoreObtainmentState>();
            _currentProvidersLock = new Mutex();
            _backgroundTask = _taskSchedulerScope.RunAsync("BackgroundTask", RunAsync, CancellationToken.None);
        }

        public void SetTracer(WorkerPoolTracer tracer)
        {
            _tracer = tracer;
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            await _taskSchedulerScope.DisposeAsync().ConfigureAwait(false);
            await _requestCollection.OnRequestsChanged.RemoveAsync(OnNotifiedRequestsChanged).ConfigureAwait(false);
            try
            {
                await _backgroundTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private Task OnNotifiedRequestsChanged(WorkerCoreRequestStatistics statistics, CancellationToken token)
        {
            _tracer?.AddTracingMessage("Notified that requests have changed.");
            _processRequestsSemaphore.Release();
            return Task.CompletedTask;
        }

        private async Task OnNotifiedProvidersChanged(WorkerCoreProviderCollectionChanged<TWorkerCore> providersInfo, CancellationToken token)
        {
            _tracer?.AddTracingMessage("Notified that providers have changed.");

            using var _ = await _currentProvidersLock.WaitAsync(token).ConfigureAwait(false);

            if (providersInfo.AddedProvider != null)
            {
                if (!_currentProviders.ContainsKey(providersInfo.AddedProvider))
                {
                    _currentProviders.Add(providersInfo.AddedProvider, new WorkerCoreObtainmentState(this, _logger, Guid.NewGuid().ToString()));
                }
            }
            if (providersInfo.RemovedProvider != null)
            {
                if (_currentProviders.TryGetValue(providersInfo.RemovedProvider, out var state))
                {
                    await state.CancelObtainmentIfRunningAsync().ConfigureAwait(false);
                    _currentProviders.Remove(providersInfo.RemovedProvider);
                }
            }

            _processRequestsSemaphore.Release();
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // Set up our events.
            while (true)
            {
                try
                {
                    await _requestCollection.OnRequestsChanged.AddAsync(OnNotifiedRequestsChanged).ConfigureAwait(false);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, ex.Message);
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
            while (true)
            {
                try
                {
                    await _providerCollection.OnProvidersChanged.AddAsync(OnNotifiedProvidersChanged).ConfigureAwait(false);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, ex.Message);
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }

            // Get the initial providers.
            while (true)
            {
                using (await _currentProvidersLock.WaitAsync(cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        foreach (var provider in await _providerCollection.GetProvidersAsync().ConfigureAwait(false))
                        {
                            _currentProviders.Add(provider, new WorkerCoreObtainmentState(this, _logger, Guid.NewGuid().ToString()));
                        }
                        break;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCritical(ex, ex.Message);
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            // Process requests.
            while (true)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        _tracer?.AddTracingMessage("Waiting to be notified that requests have changed.");
                        await _processRequestsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                        _tracer?.AddTracingMessage("Waiting to obtain the provider lock.");
                        using (await _currentProvidersLock.WaitAsync(cancellationToken).ConfigureAwait(false))
                        {
                            // Step 1: Get the list of requests we haven't fulfilled yet.
                            int differenceCores;
                            await using ((await _requestCollection.GetAllUnfulfilledRequestsAsync(
                                _fulfillsLocalRequests ? CoreFulfillerConstraint.All : CoreFulfillerConstraint.LocalPreferredAndRemote,
                                cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var initiallyUnfulfilledRequests).ConfigureAwait(false))
                            {
                                // Step 2: Fulfill any unfulfilled requests that we can immediately fulfill
                                // now (because our obtainment tasks have obtained cores).
                                var remainingUnfulfilledRequests = new List<IWorkerCoreRequest<TWorkerCore>>();
                                foreach (var request in initiallyUnfulfilledRequests.Requests)
                                {
                                retryCore:
                                    var coreAvailableToFulfillRequest = _currentProviders.Values.FirstOrDefault(x => x.HasObtainedCore);
                                    if (coreAvailableToFulfillRequest == null)
                                    {
                                        // No more cores immediately available.
                                        remainingUnfulfilledRequests.Add(request.Request);
                                    }
                                    else
                                    {
                                        // Fulfill the request.
                                        var core = coreAvailableToFulfillRequest.TakeObtainedCore();
                                        if (core is IWorkerCoreWithLiveness coreWithLiveness &&
                                            !(await coreWithLiveness.IsAliveAsync(cancellationToken).ConfigureAwait(false)))
                                        {
                                            // This core is dead. Do not use it.
                                            await core.DisposeAsync().ConfigureAwait(false);
                                            goto retryCore;
                                        }
                                        else
                                        {
                                            // Assign the core.
                                            await request.FulfillRequestAsync(core).ConfigureAwait(false);
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
                                differenceCores = obtainmentTargetCores - obtainmentCurrentCores;
                            }

                            _tracer?.AddTracingMessage($"Determined that the difference in cores is {differenceCores}.");

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
                                        _tracer?.AddTracingMessage($"{provider.Value}: Starting obtainment of core.");

                                        // Try to obtain the core.
                                        var didAssignObtainedCore = false;
                                        TWorkerCore? obtainedCore = default;
                                        try
                                        {
                                            obtainedCore = await provider.Key.RequestCoreAsync(cancellationToken).ConfigureAwait(false);
                                        }
                                        catch (Exception ex)
                                        {
                                            _tracer?.AddTracingMessage($"{provider.Value}: Exception during RequestCoreAsync: {ex}");
                                        }

                                        // Evaluate whether or not the obtainment was successful inside the lock
                                        // to prevent weird states emerging as request notifications come in at
                                        // various points.
                                        //
                                        // @note: We use CancellationToken.None here because we need to obtain the
                                        // lock even to do cleanup operations without the state getting weird.
                                        using (await _currentProvidersLock.WaitAsync(CancellationToken.None).ConfigureAwait(false))
                                        {
                                            if (cancellationToken.IsCancellationRequested)
                                            {
                                                if (obtainedCore != null)
                                                {
                                                    await obtainedCore.DisposeAsync().ConfigureAwait(false);
                                                }
                                                cancellationToken.ThrowIfCancellationRequested();
                                            }
                                            else
                                            {
                                                if (obtainedCore == null)
                                                {
                                                    _tracer?.AddTracingMessage($"{provider.Value}: Failed to obtain core.");
                                                    _tracer?.AddTracingMessage($"{provider.Value}: Indicating obtainment failed.");
                                                    provider.Value.IndicateObtainmentFailed();
                                                    _tracer?.AddTracingMessage($"{provider.Value}: Indicated obtainment failed.");
                                                }
                                                else
                                                {
                                                    _tracer?.AddTracingMessage($"{provider.Value}: Obtained core {obtainedCore}, but yet to accept.");
                                                    try
                                                    {
                                                        _tracer?.AddTracingMessage($"{provider.Value}: Accepting obtained core {obtainedCore}.");
                                                        provider.Value.AcceptObtainedCore(obtainedCore);
                                                        didAssignObtainedCore = true;
                                                        _tracer?.AddTracingMessage($"{provider.Value}: Accepted obtained core {obtainedCore}.");
                                                    }
                                                    finally
                                                    {
                                                        if (!didAssignObtainedCore)
                                                        {
                                                            await obtainedCore.DisposeAsync().ConfigureAwait(false);

                                                            _tracer?.AddTracingMessage($"{provider.Value}: Indicating obtainment failed.");
                                                            provider.Value.IndicateObtainmentFailed();
                                                            _tracer?.AddTracingMessage($"{provider.Value}: Indicated obtainment failed.");
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        // Reprocess requests regardless of whether we succeed to ensure that the 
                                        // state stabilises.
                                        _processRequestsSemaphore.Release();
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
                                    _tracer?.AddTracingMessage("Cancelling core obtainment.");
                                    var provider = workingProviders[i];
                                    await provider.Value.CancelObtainingCoreAsync().ConfigureAwait(false);
                                }
                            }
                        }
                    }

                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, ex.Message);
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
