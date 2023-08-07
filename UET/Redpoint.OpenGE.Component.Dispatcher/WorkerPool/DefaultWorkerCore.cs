namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Protocol;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultWorkerCore : IWorkerCore
    {
        private readonly WorkerSubpool _subpool;
        private readonly ILogger _logger;
        private readonly WorkerState _worker;
        private readonly BufferedAsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> _request;
        private readonly string _workerMachineName;
        private readonly int _workerCoreNumber;
        private readonly SemaphoreSlim _deathSemaphore;
        private bool _dead;

        public DefaultWorkerCore(
            WorkerSubpool subpool,
            ILogger logger,
            WorkerState worker,
            AsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> request,
            string workerMachineName,
            int workerCoreNumber)
        {
            _deathSemaphore = new SemaphoreSlim(1);
            _subpool = subpool;
            _logger = logger;
            _worker = worker;
            _request = new BufferedAsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse>(
                logger,
                request);
            _request.OnException = async ex =>
            {
                if (ex is RpcException)
                {
                    await DisposeAsync();
                }
            };
            _request.StartObserving();
            _workerMachineName = workerMachineName;
            _workerCoreNumber = workerCoreNumber;
        }

        public BufferedAsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> Request => _request;

        public string WorkerMachineName => _workerMachineName;

        public int WorkerCoreNumber => _workerCoreNumber;

        public bool Dead => _dead;

        public async ValueTask DisposeAsync()
        {
            await _deathSemaphore.WaitAsync();
            try
            {
                if (_dead)
                {
                    return;
                }
                _logger.LogTrace($"Core {_workerCoreNumber} on {_workerMachineName} is being disposed...");
                _dead = true;
                await _request.DisposeAsync();
                try
                {
                    await _request.RequestStream.CompleteAsync();
                }
                catch { }
                _subpool.DecrementCoresRequested();
                _subpool.DecrementCoresReserved();
                await _worker.RemoveReservationByCoreAsync(this);
                _logger.LogTrace($"Core {_workerCoreNumber} on {_workerMachineName} has been disposed.");
            }
            finally
            {
                _deathSemaphore.Release();
            }
        }
    }
}
