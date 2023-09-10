namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.Concurrency;
    using System;
    using System.Threading.Tasks;

    public partial class WorkerCoreRequestCollection<TWorkerCore>
    {
        internal class WorkerCoreRequest : IWorkerCoreRequest<TWorkerCore>
        {
            private readonly Gate _requestCompleted;
            private readonly WorkerCoreRequestCollection<TWorkerCore> _collection;
            private readonly DateTime _dateRequestedUtc;
            private TWorkerCore? _assignedCore;

            public WorkerCoreRequest(
                WorkerCoreRequestCollection<TWorkerCore> collection,
                CoreAllocationPreference corePreference)
            {
                _requestCompleted = new Gate();
                _collection = collection;
                _dateRequestedUtc = DateTime.UtcNow;
                CorePreference = corePreference;
            }

            public CoreAllocationPreference CorePreference { get; }

            public TWorkerCore? AssignedCore => _assignedCore;

            public DateTime DateRequestedUtc => _dateRequestedUtc;

            internal async Task FulfillRequestWithinLockAsync(TWorkerCore core)
            {
                if (_assignedCore != null)
                {
                    throw new InvalidOperationException();
                }

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

            public async ValueTask DisposeAsync()
            {
                if (_assignedCore != null)
                {
                    await _assignedCore.DisposeAsync();
                }

                using var _ = await _collection._requestLock.WaitAsync(CancellationToken.None);

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
