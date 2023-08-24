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
        private WorkerCoreRequestStatistics? _lastStatistics;

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
            _lastStatistics = statistics;
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
                    _lastStatistics = await _requestCollection.GetCurrentStatisticsAsync(_disposedCts.Token);

                    while (!_disposedCts.IsCancellationRequested)
                    {
                        await _processRequestsSemaphore.WaitAsync(_disposedCts.Token);

                        var unfulfilledRequestCount = _fulfillsLocalRequests
                            ? _lastStatistics.UnfulfilledLocalRequests
                            : _lastStatistics.UnfulfilledRemotableRequests;
                        if (unfulfilledRequestCount > 0)
                        {
                            var unfulfilledRequests = _fulfillsLocalRequests
                                ? await _requestCollection.GetUnfulfilledLocalRequestsAsync(_disposedCts.Token)
                                : await _requestCollection.GetUnfulfilledRemotableRequestsAsync(_disposedCts.Token);
                            var nextUnfulfilledRequest = unfulfilledRequests.FirstOrDefault();
                            if (nextUnfulfilledRequest != null)
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
