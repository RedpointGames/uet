namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class SingleSourceWorkerCoreRequestFulfiller<TWorkerCore> : IAsyncDisposable where TWorkerCore : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly WorkerCoreRequestCollection<TWorkerCore> _requestCollection;
        private readonly IWorkerCoreProvider<TWorkerCore> _coreProvider;
        private readonly bool _fulfillsLocalRequests;
        private readonly SemaphoreSlim _processRequestsSemaphore;
        private readonly Task _backgroundTask;
        private readonly CancellationTokenSource _disposedCts;

        public SingleSourceWorkerCoreRequestFulfiller(
            ILogger logger,
            WorkerCoreRequestCollection<TWorkerCore> requestCollection,
            IWorkerCoreProvider<TWorkerCore> coreProvider,
            bool canFulfillLocalRequests)
        {
            _logger = logger;
            _requestCollection = requestCollection;
            _coreProvider = coreProvider;
            _fulfillsLocalRequests = canFulfillLocalRequests;
            _disposedCts = new CancellationTokenSource();
            _processRequestsSemaphore = new SemaphoreSlim(1);
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

            // Process requests.
            while (true)
            {
                try
                {
                    while (!_disposedCts.IsCancellationRequested)
                    {
                        await _processRequestsSemaphore.WaitAsync(_disposedCts.Token);

                        async Task FulfillRequest(IWorkerCoreRequestLock<TWorkerCore> nextUnfulfilledRequest)
                        {
                            await using (nextUnfulfilledRequest)
                            {
                                try
                                {
                                    var nextAvailableCore = await _coreProvider.RequestCoreAsync(_disposedCts.Token);
                                    var didFulfill = false;
                                    try
                                    {
                                        await nextUnfulfilledRequest.FulfillRequestAsync(nextAvailableCore);
                                        didFulfill = true;
                                    }
                                    finally
                                    {
                                        if (!didFulfill)
                                        {
                                            await nextAvailableCore.DisposeAsync();
                                        }
                                    }
                                }
                                finally
                                {
                                    // We failed to acquire a core.
                                    _processRequestsSemaphore.Release();
                                }
                            }
                        }

                        if (_fulfillsLocalRequests)
                        {
                            // Try to get local only requests first.
                            var nextUnfulfilledLocalOnlyRequest = await _requestCollection.GetNextUnfulfilledRequestAsync(
                                CoreFulfillerConstraint.LocalRequiredAndPreferred,
                                _disposedCts.Token);
                            if (nextUnfulfilledLocalOnlyRequest != null)
                            {
                                await FulfillRequest(nextUnfulfilledLocalOnlyRequest);
                                continue;
                            }
                            else
                            {
                                // We don't have a local only request. Wait a little bit to allow other fulfillers
                                // to pick up remotable requests (since we'd prefer to run them not on the local core).
                                await Task.Delay(100);
                            }
                        }

                        // Get any unfulfilled request.
                        var nextUnfulfilledRequest = await _requestCollection.GetNextUnfulfilledRequestAsync(
                            _fulfillsLocalRequests ? CoreFulfillerConstraint.All : CoreFulfillerConstraint.LocalPreferredAndRemote,
                            _disposedCts.Token);
                        if (nextUnfulfilledRequest != null)
                        {
                            await FulfillRequest(nextUnfulfilledRequest);
                            continue;
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
