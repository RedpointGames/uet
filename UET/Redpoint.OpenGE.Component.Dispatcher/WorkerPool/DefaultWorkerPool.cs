namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.OpenGE.Protocol;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultWorkerPool : IWorkerPool
    {
        private readonly TaskApi.TaskApiClient _localWorker;

        public DefaultWorkerPool(TaskApi.TaskApiClient localWorker)
        {
            _localWorker = localWorker;
        }

        public Task<IWorkerCore> ReserveLocalCoreAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IWorkerCore> ReserveRemoteOrLocalCoreAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}
