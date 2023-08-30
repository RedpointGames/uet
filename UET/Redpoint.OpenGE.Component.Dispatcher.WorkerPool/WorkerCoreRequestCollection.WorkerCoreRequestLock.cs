namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System;
    using System.Threading.Tasks;

    public partial class WorkerCoreRequestCollection<TWorkerCore>
    {
        private class WorkerCoreRequestLock : IWorkerCoreRequestLock<TWorkerCore>
        {
            private readonly WorkerCoreRequestCollection<TWorkerCore> _collection;
            private readonly WorkerCoreRequestCollection<TWorkerCore>.WorkerCoreRequest _request;

            public WorkerCoreRequestLock(
                WorkerCoreRequestCollection<TWorkerCore> collection,
                WorkerCoreRequest request)
            {
                _collection = collection;
                _request = request;
            }

            public IWorkerCoreRequest<TWorkerCore> Request => _request;

            public async Task FulfillRequestAsync(TWorkerCore core)
            {
                using var _ = await _collection._requestLock.WaitAsync(CancellationToken.None);
                await _request.FulfillRequestWithinLockAsync(core);
            }

            public async ValueTask DisposeAsync()
            {
                using var _ = await _collection._requestLock.WaitAsync(CancellationToken.None);
                _request.LockAcquired = false;
            }
        }
    }
}
