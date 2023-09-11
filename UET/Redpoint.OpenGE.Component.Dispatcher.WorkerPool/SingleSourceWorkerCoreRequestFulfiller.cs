namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.Tasks;
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    public class SingleSourceWorkerCoreRequestFulfiller<TWorkerCore> : IAsyncDisposable, IWorkerPoolTracerAssignable where TWorkerCore : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly bool _enableTracing;
        private readonly ITaskSchedulerScope _taskSchedulerScope;
        private readonly WorkerCoreRequestCollection<TWorkerCore> _requestCollection;
        private readonly IWorkerCoreProvider<TWorkerCore> _coreProvider;
        private readonly bool _fulfillsLocalRequests;
        private readonly int _remoteDelayMilliseconds;
        private readonly Semaphore _processRequestsSemaphore;
        private readonly ConcurrentQueue<TWorkerCore> _coresAcquired;
        private long _coreAcquiringCount;
        private readonly Mutex _requestProcessingLock;
        private readonly Task _backgroundTask;
        private WorkerPoolTracer? _tracer;

        public SingleSourceWorkerCoreRequestFulfiller(
            ILogger logger,
            ITaskScheduler taskScheduler,
            WorkerCoreRequestCollection<TWorkerCore> requestCollection,
            IWorkerCoreProvider<TWorkerCore> coreProvider,
            bool canFulfillLocalRequests,
            int remoteDelayMilliseconds)
        {
            _logger = logger;
            _enableTracing = _logger.IsEnabled(LogLevel.Trace);
            _taskSchedulerScope = taskScheduler.CreateSchedulerScope("SingleSourceWorkerCoreRequestFulfiller", CancellationToken.None);
            _requestCollection = requestCollection;
            _coreProvider = coreProvider;
            _fulfillsLocalRequests = canFulfillLocalRequests;
            _remoteDelayMilliseconds = remoteDelayMilliseconds;
            _processRequestsSemaphore = new Semaphore(1);
            _coresAcquired = new ConcurrentQueue<TWorkerCore>();
            _coreAcquiringCount = 0;
            _requestProcessingLock = new Mutex();
            _backgroundTask = _taskSchedulerScope.RunAsync("BackgroundTask", CancellationToken.None, RunAsync);
        }

        public class Statistics
        {
            public required long CoreAcquiringCount;
            public required int CoresCurrentlyAcquiredCount;
            public required TWorkerCore[] CoresCurrentlyAcquired;
        }

        public Statistics GetStatistics()
        {
            return new Statistics
            {
                CoreAcquiringCount = (int)Interlocked.Read(ref _coreAcquiringCount),
                CoresCurrentlyAcquiredCount = _coresAcquired.Count,
                CoresCurrentlyAcquired = _coresAcquired.ToArray(),
            };
        }

        public void SetTracer(WorkerPoolTracer tracer)
        {
            _tracer = tracer;
        }

        public async ValueTask DisposeAsync()
        {
            await _taskSchedulerScope.DisposeAsync();
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
            if (_enableTracing)
            {
                _logger.LogTrace($"{nameof(SingleSourceWorkerCoreRequestFulfiller<TWorkerCore>)}.{nameof(OnNotifiedRequestsChanged)}: Notified that requests have changed.");
            }
            _processRequestsSemaphore.Release();
            return Task.CompletedTask;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            // Set up our events.
            while (true)
            {
                try
                {
                    await _requestCollection.OnRequestsChanged.AddAsync(OnNotifiedRequestsChanged);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, ex.Message);
                    await Task.Delay(1000);
                }
            }

            // Process requests.
            while (true)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (_enableTracing)
                        {
                            _logger.LogTrace($"{nameof(SingleSourceWorkerCoreRequestFulfiller<TWorkerCore>)}.{nameof(RunAsync)}: Waiting to be notified that requests have changed.");
                        }
                        if (_remoteDelayMilliseconds > 0)
                        {
                            // Wait until we either get a notification, or until the remote delay
                            // period has elapsed.
                            try
                            {
                                await _processRequestsSemaphore.WaitAsync(CancellationTokenSource.CreateLinkedTokenSource(
                                    new CancellationTokenSource(_remoteDelayMilliseconds).Token,
                                    cancellationToken).Token);
                            }
                            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                            {
                                // This is a normal timeout for remote delay checking; ignore the
                                // exception and proceed as if we had been notified.
                            }
                        }
                        else
                        {
                            // We never defer remote preferred requests in this case, so we only
                            // need to wait until we're notified.
                            await _processRequestsSemaphore.WaitAsync(cancellationToken);
                        }

                        if (_enableTracing)
                        {
                            _logger.LogTrace($"{nameof(SingleSourceWorkerCoreRequestFulfiller<TWorkerCore>)}.{nameof(RunAsync)}: Waiting to obtain the request processing lock.");
                        }
                        using (await _requestProcessingLock.WaitAsync(cancellationToken))
                        {
                            // Step 1: Get the list of local only requests we haven't fulfilled yet.
                            int pendingRequestCount = 0;
                            var seenRequests = new HashSet<IWorkerCoreRequest<TWorkerCore>>();
                            if (_fulfillsLocalRequests)
                            {
                                await using (var unfulfilledRequests = await _requestCollection
                                    .GetAllUnfulfilledRequestsAsync(
                                        CoreFulfillerConstraint.LocalRequiredAndPreferred,
                                        cancellationToken))
                                {
                                    // Step 2: Fulfill any unfulfilled requests that we can immediately fulfill
                                    // now (because our background tasks have obtained cores).
                                    foreach (var request in unfulfilledRequests.Requests)
                                    {
                                        seenRequests.Add(request.Request);
                                    retryCore:
                                        if (!_coresAcquired.TryDequeue(out var core))
                                        {
                                            // No more cores immediately available.
                                            pendingRequestCount++;
                                        }
                                        else
                                        {
                                            // Fulfill the request.
                                            if (core is IWorkerCoreWithLiveness coreWithLiveness &&
                                                !(await coreWithLiveness.IsAliveAsync(cancellationToken)))
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
                                }
                            }

                            // Step 3: Get the list of remote preferred requests we haven't fulfilled yet,
                            // only if the requests are old enough.
                            await using (var unfulfilledRequests = await _requestCollection
                                .GetAllUnfulfilledRequestsAsync(
                                    _fulfillsLocalRequests
                                        ? CoreFulfillerConstraint.All
                                        : CoreFulfillerConstraint.LocalPreferredAndRemote,
                                    cancellationToken))
                            {
                                foreach (var request in unfulfilledRequests.Requests)
                                {
                                    if (seenRequests.Contains(request.Request))
                                    {
                                        // We already handled this request earlier in the local-only check.
                                        continue;
                                    }
                                    if ((DateTime.UtcNow - request.Request.DateRequestedUtc).TotalMilliseconds < _remoteDelayMilliseconds)
                                    {
                                        // We're not allowed to fulfill this request yet; we want another fulfiller
                                        // to have the opportunity to satisify the request first.
                                        continue;
                                    }

                                retryCore:
                                    if (!_coresAcquired.TryDequeue(out var core))
                                    {
                                        // No more cores immediately available.
                                        pendingRequestCount++;
                                    }
                                    else
                                    {
                                        // Fulfill the request.
                                        if (core is IWorkerCoreWithLiveness coreWithLiveness &&
                                            !(await coreWithLiveness.IsAliveAsync(cancellationToken)))
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
                            }
                            if (_enableTracing)
                            {
                                _logger.LogTrace($"{nameof(SingleSourceWorkerCoreRequestFulfiller<TWorkerCore>)}.{nameof(RunAsync)}: There are {pendingRequestCount} pending requests.");
                            }

                            // Step 4: Compute how many cores we need to be requesting, based on
                            // how many are in-flight and how many we're lacking.
                            int differenceCores = pendingRequestCount - (int)Interlocked.Read(ref _coreAcquiringCount);
                            if (differenceCores > 0)
                            {
                                // Start background tasks to obtain more cores.
                                for (int i = 0; i < differenceCores; i++)
                                {
                                    if (_enableTracing)
                                    {
                                        _logger.LogTrace($"{nameof(SingleSourceWorkerCoreRequestFulfiller<TWorkerCore>)}.{nameof(RunAsync)}: Scheduling obtainment of core [current = {i}/{differenceCores}].");
                                    }
                                    Interlocked.Increment(ref _coreAcquiringCount);
                                    _ = _taskSchedulerScope.RunAsync(
                                        "ObtainCore",
                                        cancellationToken,
                                        async (cancellationToken) =>
                                        {
                                            try
                                            {
                                                if (_enableTracing)
                                                {
                                                    _logger.LogTrace($"{nameof(SingleSourceWorkerCoreRequestFulfiller<TWorkerCore>)}.{nameof(RunAsync)}: Starting obtainment of core.");
                                                }

                                                // Try to obtain the core.
                                                TWorkerCore? obtainedCore = default;
                                                try
                                                {
                                                    obtainedCore = await _coreProvider.RequestCoreAsync(cancellationToken);
                                                }
                                                catch (Exception ex)
                                                {
                                                    if (_enableTracing)
                                                    {
                                                        _logger.LogTrace($"{nameof(SingleSourceWorkerCoreRequestFulfiller<TWorkerCore>)}.{nameof(RunAsync)}: Exception during RequestCoreAsync: {ex}");
                                                    }
                                                }

                                                // If we got a core, put it into the queue.
                                                if (obtainedCore != null)
                                                {
                                                    _coresAcquired.Enqueue(obtainedCore);
                                                }
                                            }
                                            finally
                                            {
                                                Interlocked.Decrement(ref _coreAcquiringCount);
                                                _processRequestsSemaphore.Release();
                                            }
                                        });
                                }
                            }
                            else
                            {
                                // Unlike the multiple fulfiller, we don't cancel core requests because we're
                                // not racing fulfillment from multiple sources. If a local core we obtain
                                // doesn't get used, it'll either get picked up by a later task or timeout
                                // and be available again later.
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
                    await Task.Delay(1000);
                }
            }
        }
    }
}
