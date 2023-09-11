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
            if (taskScheduler == null) throw new ArgumentNullException(nameof(taskScheduler));

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
            _backgroundTask = _taskSchedulerScope.RunAsync("BackgroundTask", RunAsync, CancellationToken.None);
        }

        public SingleSourceWorkerCoreRequestFulfillerStatistics<TWorkerCore> GetStatistics()
        {
            return new SingleSourceWorkerCoreRequestFulfillerStatistics<TWorkerCore>
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
                                    cancellationToken).Token).ConfigureAwait(false);
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
                            await _processRequestsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                        }

                        if (_enableTracing)
                        {
                            _logger.LogTrace($"{nameof(SingleSourceWorkerCoreRequestFulfiller<TWorkerCore>)}.{nameof(RunAsync)}: Waiting to obtain the request processing lock.");
                        }
                        using (await _requestProcessingLock.WaitAsync(cancellationToken).ConfigureAwait(false))
                        {
                            // Step 1: Get the list of local only requests we haven't fulfilled yet.
                            int pendingRequestCount = 0;
                            var seenRequests = new HashSet<IWorkerCoreRequest<TWorkerCore>>();
                            if (_fulfillsLocalRequests)
                            {
                                await using ((await _requestCollection
                                    .GetAllUnfulfilledRequestsAsync(
                                        CoreFulfillerConstraint.LocalRequiredAndPreferred,
                                        cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var unfulfilledRequests).ConfigureAwait(false))
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
                                }
                            }

                            // Step 3: Get the list of remote preferred requests we haven't fulfilled yet,
                            // only if the requests are old enough.
                            await using ((await _requestCollection
                                .GetAllUnfulfilledRequestsAsync(
                                    _fulfillsLocalRequests
                                        ? CoreFulfillerConstraint.All
                                        : CoreFulfillerConstraint.LocalPreferredAndRemote,
                                    cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var unfulfilledRequests).ConfigureAwait(false))
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
                                                    obtainedCore = await _coreProvider.RequestCoreAsync(cancellationToken).ConfigureAwait(false);
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
                                        },
                                        cancellationToken);
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
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
