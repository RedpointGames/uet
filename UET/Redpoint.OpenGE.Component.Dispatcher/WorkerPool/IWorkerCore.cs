namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Threading.Tasks;

    internal interface IWorkerCore : IAsyncDisposable
    {
    }

    internal class RemoteWorkerCore : IWorkerCore
    {
        private readonly DefaultWorkerPool _pool;
        private readonly RemoteWorkerState _worker;
        private readonly AsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> _request;
        private readonly SemaphoreSlim _callSemaphore;

        public RemoteWorkerCore(
            DefaultWorkerPool pool,
            RemoteWorkerState worker, 
            AsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> request)
        {
            _pool = pool;
            _worker = worker;
            _request = request;
            _callSemaphore = new SemaphoreSlim(1);
        }

        public async ValueTask DisposeAsync()
        {
            await _request.RequestStream.CompleteAsync();
            Interlocked.Decrement(ref _pool._remoteCoresReserved);
            await _worker.RemoveReservationByCoreAsync(this);
        }
    }
}
