namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public partial class WorkerCoreRequestCollection<TWorkerCore>
    {
        private class WorkerCoreRequestCollectionLock : IWorkerCoreRequestCollectionLock<TWorkerCore>
        {
            private readonly IAcquiredLock _lock;
            private readonly IReadOnlyList<IWorkerCoreRequestLock<TWorkerCore>> _requests;
            private bool _disposed;

            public WorkerCoreRequestCollectionLock(
                IAcquiredLock @lock,
                IReadOnlyList<IWorkerCoreRequest<TWorkerCore>> requests)
            {
                _lock = @lock;
                _requests = requests
                    .Select(x => new WorkerCoreRequestLockWithinCollection(this, x))
                    .Cast<IWorkerCoreRequestLock<TWorkerCore>>()
                    .ToList();
            }

            public IEnumerable<IWorkerCoreRequestLock<TWorkerCore>> Requests => _requests;

            public ValueTask DisposeAsync()
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                _disposed = true;
                _lock.Dispose();
                return ValueTask.CompletedTask;
            }

            private class WorkerCoreRequestLockWithinCollection : IWorkerCoreRequestLock<TWorkerCore>
            {
                private WorkerCoreRequestCollectionLock _lock;
                private IWorkerCoreRequest<TWorkerCore> _request;

                public WorkerCoreRequestLockWithinCollection(
                    WorkerCoreRequestCollectionLock @lock,
                    IWorkerCoreRequest<TWorkerCore> request)
                {
                    _lock = @lock;
                    _request = request;
                }

                public IWorkerCoreRequest<TWorkerCore> Request => _request;

                public async Task FulfillRequestAsync(TWorkerCore core)
                {
                    ObjectDisposedException.ThrowIf(_lock._disposed, _lock);

                    await ((WorkerCoreRequest<TWorkerCore>)_request).FulfillRequestWithinLockAsync(core).ConfigureAwait(false);
                }

                public ValueTask DisposeAsync()
                {
                    ObjectDisposedException.ThrowIf(_lock._disposed, _lock);

                    return ValueTask.CompletedTask;
                }
            }
        }
    }
}
