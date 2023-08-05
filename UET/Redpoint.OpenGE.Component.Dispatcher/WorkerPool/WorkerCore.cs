namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal class WorkerCore : IWorkerCore
    {
        private readonly WorkerSubpool _subpool;
        private readonly WorkerState _worker;
        private readonly AsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> _request;
        private readonly string _workerMachineName;
        private readonly int _workerCoreNumber;

        public WorkerCore(
            WorkerSubpool subpool,
            WorkerState worker,
            AsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> request,
            string workerMachineName,
            int workerCoreNumber)
        {
            _subpool = subpool;
            _worker = worker;
            _request = request;
            _workerMachineName = workerMachineName;
            _workerCoreNumber = workerCoreNumber;
        }

        public AsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> Request => _request;

        public string WorkerMachineName => _workerMachineName;

        public int WorkerCoreNumber => _workerCoreNumber;

        public async ValueTask DisposeAsync()
        {
            await _request.RequestStream.CompleteAsync();
            Interlocked.Decrement(ref _subpool._coresReserved);
            await _worker.RemoveReservationByCoreAsync(this);
        }
    }
}
