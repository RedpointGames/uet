namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal class WorkerCoreRequestCollection<TWorkerCore> where TWorkerCore : IAsyncDisposable
    {
        private readonly List<WorkerCoreRequest> _requests;
        private readonly SemaphoreSlim _requestLock;
        private readonly AsyncEvent<WorkerCoreRequestStatistics> _onRequestsChanged;

        public WorkerCoreRequestCollection()
        {
            _requests = new List<WorkerCoreRequest>();
            _requestLock = new SemaphoreSlim(1);
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
                if (request.RequireLocal)
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
            await _requestLock.WaitAsync(cancellationToken);
            try
            {
                return ObtainStatisticsWithinLock();
            }
            finally
            {
                _requestLock.Release();
            }
        }

        public async Task<IReadOnlyList<IWorkerCoreRequest<TWorkerCore>>> GetUnfulfilledLocalRequestsAsync(CancellationToken cancellationToken)
        {
            await _requestLock.WaitAsync(cancellationToken);
            try
            {
                return _requests
                    .Where(x => x.RequireLocal && x.AssignedCore == null)
                    .ToList();
            }
            finally
            {
                _requestLock.Release();
            }
        }

        public async Task<IReadOnlyList<IWorkerCoreRequest<TWorkerCore>>> GetUnfulfilledRemotableRequestsAsync(CancellationToken cancellationToken)
        {
            await _requestLock.WaitAsync(cancellationToken);
            try
            {
                return _requests
                    .Where(x => !x.RequireLocal && x.AssignedCore == null)
                    .ToList();
            }
            finally
            {
                _requestLock.Release();
            }
        }

        public async Task<IWorkerCoreRequest<TWorkerCore>> CreateFulfilledRequestAsync(
            bool requireLocal,
            CancellationToken cancellationToken)
        {
            var didFulfill = false;
            var request = await CreateUnfulfilledRequestAsync(requireLocal, cancellationToken);
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
            bool requireLocal,
            CancellationToken cancellationToken)
        {
            await _requestLock.WaitAsync(cancellationToken);
            try
            {
                var newRequest = new WorkerCoreRequest(this, requireLocal);
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
            finally
            {
                _requestLock.Release();
            }
        }

        private class WorkerCoreRequest : IWorkerCoreRequest<TWorkerCore>
        {
            private readonly Gate _requestCompleted;
            private readonly WorkerCoreRequestCollection<TWorkerCore> _collection;
            private TWorkerCore? _assignedCore;

            public WorkerCoreRequest(
                WorkerCoreRequestCollection<TWorkerCore> collection,
                bool requireLocal)
            {
                _requestCompleted = new Gate();
                _collection = collection;
                RequireLocal = requireLocal;
            }

            public bool RequireLocal { get; }

            public TWorkerCore? AssignedCore => _assignedCore;

            public async Task FulfillRequestAsync(TWorkerCore core)
            {
                if (_assignedCore != null)
                {
                    throw new InvalidOperationException();
                }
                await _collection._requestLock.WaitAsync(CancellationToken.None);
                try
                {
                    _assignedCore = core;
                    try
                    {
                        await _collection._onRequestsChanged.BroadcastAsync(
                            _collection.ObtainStatisticsWithinLock(),
                            CancellationToken.None);
                    }
                    catch
                    {
                    }
                    _requestCompleted.Open();
                }
                finally
                {
                    _collection._requestLock.Release();
                }
            }

            public async ValueTask DisposeAsync()
            {
                if (_assignedCore != null)
                {
                    await _assignedCore.DisposeAsync();
                }
                await _collection._requestLock.WaitAsync(CancellationToken.None);
                try
                {
                    _collection._requests.Remove(this);
                    try
                    {
                        await _collection._onRequestsChanged.BroadcastAsync(
                            _collection.ObtainStatisticsWithinLock(),
                            CancellationToken.None);
                    }
                    catch
                    {
                    }
                }
                finally
                {
                    _collection._requestLock.Release();
                }
                _requestCompleted.Open();
            }

            public async Task<TWorkerCore> WaitForCoreAsync(CancellationToken cancellationToken)
            {
                await _requestCompleted.WaitAsync(cancellationToken);
                if (_assignedCore == null)
                {
                    throw new Exception("Worker core request not fulfilled.");
                }
                return _assignedCore;
            }
        }
    }
}
