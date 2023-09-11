namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class WorkerCoreRequestCollection<TWorkerCore> : IWorkerPoolTracerAssignable where TWorkerCore : IAsyncDisposable
    {
        private readonly List<WorkerCoreRequest> _requests;
        private readonly Concurrency.Mutex _requestLock;
        private readonly AsyncEvent<WorkerCoreRequestStatistics> _onRequestsChanged;
        private WorkerPoolTracer? _tracer;

        public WorkerCoreRequestCollection()
        {
            _requests = new List<WorkerCoreRequest>();
            _requestLock = new Concurrency.Mutex();
            _onRequestsChanged = new AsyncEvent<WorkerCoreRequestStatistics>();
        }

        public async Task<WorkerCoreRequest[]> GetAllRequestsAsync()
        {
            using (await _requestLock.WaitAsync())
            {
                return _requests.ToArray();
            }
        }

        public void SetTracer(WorkerPoolTracer tracer)
        {
            _tracer = tracer;
        }

        private WorkerCoreRequestStatistics ObtainStatisticsWithinLock()
        {
            var unfulfilledLocalRequests = 0;
            var unfulfilledRemotableRequests = 0;
            var fulfilledLocalRequests = 0;
            var fulfilledRemotableRequests = 0;
            foreach (var request in _requests)
            {
                if (request.CorePreference == CoreAllocationPreference.RequireLocal)
                {
                    if (request.AssignedCore == null)
                    {
                        unfulfilledLocalRequests++;
                    }
                    else
                    {
                        fulfilledLocalRequests++;
                    }
                }
                else
                {
                    if (request.AssignedCore == null)
                    {
                        unfulfilledRemotableRequests++;
                    }
                    else
                    {
                        fulfilledRemotableRequests++;
                    }
                }
            }
            return new WorkerCoreRequestStatistics
            {
                UnfulfilledLocalRequests = unfulfilledLocalRequests,
                UnfulfilledRemotableRequests = unfulfilledRemotableRequests,
                FulfilledLocalRequests = fulfilledLocalRequests,
                FulfilledRemotableRequests = fulfilledRemotableRequests,
            };
        }

        public IAsyncEvent<WorkerCoreRequestStatistics> OnRequestsChanged => _onRequestsChanged;

        public async Task<WorkerCoreRequestStatistics> GetCurrentStatisticsAsync(CancellationToken cancellationToken)
        {
            using var _ = await _requestLock.WaitAsync(cancellationToken);
            return ObtainStatisticsWithinLock();
        }

        private Func<WorkerCoreRequest, bool> GetFilterForConstraint(CoreFulfillerConstraint fulfillerConstraint)
        {
            switch (fulfillerConstraint)
            {
                case CoreFulfillerConstraint.All:
                    return x =>
                        x.AssignedCore == null;
                case CoreFulfillerConstraint.LocalRequiredOnly:
                    return x =>
                        x.CorePreference == CoreAllocationPreference.RequireLocal &&
                        x.AssignedCore == null;
                case CoreFulfillerConstraint.LocalRequiredAndPreferred:
                    return x =>
                        (x.CorePreference == CoreAllocationPreference.RequireLocal ||
                         x.CorePreference == CoreAllocationPreference.PreferLocal) &&
                        x.AssignedCore == null;
                case CoreFulfillerConstraint.LocalPreferredAndRemote:
                    return x =>
                        x.CorePreference == CoreAllocationPreference.PreferRemote &&
                        x.AssignedCore == null;
                default:
                    throw new NotImplementedException($"Fulfiller constraint {fulfillerConstraint} not implemented");
            }
        }

        public async Task<IWorkerCoreRequestCollectionLock<TWorkerCore>> GetAllUnfulfilledRequestsAsync(
            CoreFulfillerConstraint fulfillerConstraint,
            CancellationToken cancellationToken)
        {
            var @lock = await _requestLock.WaitAsync(cancellationToken);

            _tracer?.AddTracingMessage("Getting all unfulfilled requests.");
            var handedOverLock = false;
            try
            {
                var requests = _requests
                    .Where(GetFilterForConstraint(fulfillerConstraint))
                    .ToList();
                var result = new WorkerCoreRequestCollectionLock(@lock, requests);
                handedOverLock = true;
                return result;
            }
            finally
            {
                if (!handedOverLock)
                {
                    _tracer?.AddTracingMessage("Did not acquire lock on enumerable, releasing.");
                    @lock.Dispose();
                }
            }
        }

        public async Task<IWorkerCoreRequest<TWorkerCore>> CreateFulfilledRequestAsync(
            CoreAllocationPreference corePreference,
            CancellationToken cancellationToken)
        {
            _tracer?.AddTracingMessage("Creating an unfulfilled request so we can wait on it.");
            var didFulfill = false;
            var request = await CreateUnfulfilledRequestAsync(corePreference, cancellationToken);
            try
            {
                _tracer?.AddTracingMessage("Waiting for the request to be fulfilled.");
                await request.WaitForCoreAsync(cancellationToken);
                didFulfill = true;
                return request;
            }
            finally
            {
                if (!didFulfill)
                {
                    await request.DisposeAsync();
                }
            }
        }

        public async Task<IWorkerCoreRequest<TWorkerCore>> CreateUnfulfilledRequestAsync(
            CoreAllocationPreference corePreference,
            CancellationToken cancellationToken)
        {
            using var _ = await _requestLock.WaitAsync(cancellationToken);

            _tracer?.AddTracingMessage("Creating new unfulfilled request and adding to dictionary.");
            var newRequest = new WorkerCoreRequest(this, corePreference);
            _requests.Add(newRequest);
            try
            {
                _tracer?.AddTracingMessage("Broadcasting that the list of requests has changed.");
                await _onRequestsChanged.BroadcastAsync(
                    ObtainStatisticsWithinLock(),
                    cancellationToken);
            }
            catch
            {
            }
            return newRequest;
        }
    }
}
