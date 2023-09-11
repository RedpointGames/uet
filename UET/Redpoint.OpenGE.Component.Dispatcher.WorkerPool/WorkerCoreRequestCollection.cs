namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;

    [SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "This class represents a collection of objects.")]
    public partial class WorkerCoreRequestCollection<TWorkerCore> : IWorkerPoolTracerAssignable where TWorkerCore : IAsyncDisposable
    {
        internal readonly List<WorkerCoreRequest<TWorkerCore>> _requests;
        internal readonly Concurrency.Mutex _requestLock;
        internal readonly AsyncEvent<WorkerCoreRequestStatistics> _onRequestsChanged;
        private WorkerPoolTracer? _tracer;

        public WorkerCoreRequestCollection()
        {
            _requests = new List<WorkerCoreRequest<TWorkerCore>>();
            _requestLock = new Concurrency.Mutex();
            _onRequestsChanged = new AsyncEvent<WorkerCoreRequestStatistics>();
        }

        public async Task<WorkerCoreRequest<TWorkerCore>[]> GetAllRequestsAsync()
        {
            using (await _requestLock.WaitAsync(CancellationToken.None).ConfigureAwait(false))
            {
                return _requests.ToArray();
            }
        }

        public void SetTracer(WorkerPoolTracer tracer)
        {
            _tracer = tracer;
        }

        internal WorkerCoreRequestStatistics ObtainStatisticsWithinLock()
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
            using var _ = await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            return ObtainStatisticsWithinLock();
        }

        private static Func<WorkerCoreRequest<TWorkerCore>, bool> GetFilterForConstraint(CoreFulfillerConstraint fulfillerConstraint)
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
            var @lock = await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            _tracer?.AddTracingMessage("Getting all unfulfilled requests.");
            var handedOverLock = false;
            try
            {
                var requests = _requests
                    .Where(WorkerCoreRequestCollection<TWorkerCore>.GetFilterForConstraint(fulfillerConstraint))
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
            var request = await CreateUnfulfilledRequestAsync(corePreference, cancellationToken).ConfigureAwait(false);
            try
            {
                _tracer?.AddTracingMessage("Waiting for the request to be fulfilled.");
                await request.WaitForCoreAsync(cancellationToken).ConfigureAwait(false);
                didFulfill = true;
                return request;
            }
            finally
            {
                if (!didFulfill)
                {
                    await request.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task<IWorkerCoreRequest<TWorkerCore>> CreateUnfulfilledRequestAsync(
            CoreAllocationPreference corePreference,
            CancellationToken cancellationToken)
        {
            using var _ = await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            _tracer?.AddTracingMessage("Creating new unfulfilled request and adding to dictionary.");
            var newRequest = new WorkerCoreRequest<TWorkerCore>(this, corePreference);
            _requests.Add(newRequest);
            try
            {
                _tracer?.AddTracingMessage("Broadcasting that the list of requests has changed.");
                await _onRequestsChanged.BroadcastAsync(
                    ObtainStatisticsWithinLock(),
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }
            return newRequest;
        }
    }
}
