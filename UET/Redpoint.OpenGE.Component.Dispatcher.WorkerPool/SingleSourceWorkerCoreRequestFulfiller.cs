namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Tasks;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class SingleSourceWorkerCoreRequestFulfiller<TWorkerCore> : IAsyncDisposable, IWorkerPoolTracerAssignable where TWorkerCore : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly ITaskSchedulerScope _taskSchedulerScope;
        private readonly WorkerCoreRequestCollection<TWorkerCore> _requestCollection;
        private readonly IWorkerCoreProvider<TWorkerCore> _coreProvider;
        private readonly bool _fulfillsLocalRequests;
        private readonly int _remoteDelayMilliseconds;
        private readonly SemaphoreSlim _processRequestsSemaphore;
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
            _taskSchedulerScope = taskScheduler.CreateSchedulerScope("SingleSourceWorkerCoreRequestFulfiller", CancellationToken.None);
            _requestCollection = requestCollection;
            _coreProvider = coreProvider;
            _fulfillsLocalRequests = canFulfillLocalRequests;
            _remoteDelayMilliseconds = remoteDelayMilliseconds;
            _processRequestsSemaphore = new SemaphoreSlim(1);
            _backgroundTask = _taskSchedulerScope.RunAsync("BackgroundTask", CancellationToken.None, RunAsync);
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
            _tracer?.AddTracingMessage("Notified that requests have changed.");
            _processRequestsSemaphore.Release();
            return Task.CompletedTask;
        }

        private async Task FulfillRequestAsync(
            IWorkerCoreRequestLock<TWorkerCore> request,
            CancellationToken cancellationToken)
        {
            await using (request)
            {
                _tracer?.AddTracingMessage("Starting obtainment of core.");

            retryCore:
                // Obtain the core.
                var core = await _coreProvider.RequestCoreAsync(cancellationToken);

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

            // Reprocess requests regardless of whether we succeed to ensure that the 
            // state stabilises.
            _processRequestsSemaphore.Release();
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
                        _tracer?.AddTracingMessage("Waiting to be notified that requests have changed.");
                        await _processRequestsSemaphore.WaitAsync(cancellationToken);

                        // Try to get local only requests first.
                        if (_fulfillsLocalRequests)
                        {
                            _tracer?.AddTracingMessage("Obtaining the next local required or local preferred request.");
                            var nextUnfulfilledLocalOnlyRequest = await _requestCollection.GetNextUnfulfilledRequestAsync(
                                CoreFulfillerConstraint.LocalRequiredAndPreferred,
                                cancellationToken);
                            if (nextUnfulfilledLocalOnlyRequest != null)
                            {
                                _tracer?.AddTracingMessage("Fulfilling local required or local preferred request.");
                                _ = _taskSchedulerScope.RunAsync(
                                    "FulfillLocalRequest",
                                    cancellationToken,
                                    async (cancellationToken) =>
                                    {
                                        await FulfillRequestAsync(
                                            nextUnfulfilledLocalOnlyRequest,
                                            cancellationToken);
                                    });
                                continue;
                            }
                            else
                            {
                                // We don't have a local only request. Wait a little bit to allow other fulfillers
                                // to pick up remotable requests (since we'd prefer to run them not on the local core).
                                await Task.Delay(_remoteDelayMilliseconds);
                            }
                        }

                        // Get any unfulfilled request.
                        _tracer?.AddTracingMessage("Obtaining the next request.");
                        var nextUnfulfilledRequest = await _requestCollection.GetNextUnfulfilledRequestAsync(
                            _fulfillsLocalRequests ? CoreFulfillerConstraint.All : CoreFulfillerConstraint.LocalPreferredAndRemote,
                            cancellationToken);
                        if (nextUnfulfilledRequest != null)
                        {
                            _tracer?.AddTracingMessage("Fulfilling request.");
                            _ = _taskSchedulerScope.RunAsync(
                                "FulfillRequest",
                                cancellationToken,
                                async (cancellationToken) =>
                                {
                                    await FulfillRequestAsync(
                                        nextUnfulfilledRequest,
                                        cancellationToken);
                                });
                            continue;
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
