namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class WorkerCoreRequestCollection<TWorkerCore> where TWorkerCore : IAsyncDisposable
    {
        private readonly List<WorkerCoreRequest> _requests;
        private readonly MutexSlim _requestLock;
        private readonly AsyncEvent<WorkerCoreRequestStatistics> _onRequestsChanged;

        public WorkerCoreRequestCollection()
        {
            _requests = new List<WorkerCoreRequest>();
            _requestLock = new MutexSlim();
            _onRequestsChanged = new AsyncEvent<WorkerCoreRequestStatistics>();
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
                        x.AssignedCore == null &&
                        !x.LockAcquired;
                case CoreFulfillerConstraint.LocalRequiredOnly:
                    return x =>
                        x.CorePreference == CoreAllocationPreference.RequireLocal &&
                        x.AssignedCore == null &&
                        !x.LockAcquired;
                case CoreFulfillerConstraint.LocalRequiredAndPreferred:
                    return x =>
                        (x.CorePreference == CoreAllocationPreference.RequireLocal ||
                         x.CorePreference == CoreAllocationPreference.PreferLocal) &&
                        x.AssignedCore == null &&
                        !x.LockAcquired;
                case CoreFulfillerConstraint.LocalPreferredAndRemote:
                    return x =>
                        x.CorePreference == CoreAllocationPreference.PreferRemote &&
                        x.AssignedCore == null &&
                        !x.LockAcquired;
                default:
                    throw new NotImplementedException($"Fulfiller constraint {fulfillerConstraint} not implemented");
            }
        }

        public async Task<IWorkerCoreRequestLock<TWorkerCore>?> GetNextUnfulfilledRequestAsync(
            CoreFulfillerConstraint fulfillerConstraint,
            CancellationToken cancellationToken)
        {
            using var _ = await _requestLock.WaitAsync(cancellationToken);

            var nextRequest = _requests.FirstOrDefault(GetFilterForConstraint(fulfillerConstraint));
            if (nextRequest == null)
            {
                return null;
            }
            nextRequest.LockAcquired = true;
            return new WorkerCoreRequestLock(this, nextRequest);
        }

        public async Task<IWorkerCoreRequestCollectionLock<TWorkerCore>> GetAllUnfulfilledRequestsAsync(
            CoreFulfillerConstraint fulfillerConstraint,
            CancellationToken cancellationToken)
        {
            var @lock = await _requestLock.WaitAsync(cancellationToken);
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
                    @lock.Dispose();
                }
            }
        }

        public async Task<IWorkerCoreRequest<TWorkerCore>> CreateFulfilledRequestAsync(
            CoreAllocationPreference corePreference,
            CancellationToken cancellationToken)
        {
            var didFulfill = false;
            var request = await CreateUnfulfilledRequestAsync(corePreference, cancellationToken);
            try
            {
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

            var newRequest = new WorkerCoreRequest(this, corePreference);
            _requests.Add(newRequest);
            try
            {
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
