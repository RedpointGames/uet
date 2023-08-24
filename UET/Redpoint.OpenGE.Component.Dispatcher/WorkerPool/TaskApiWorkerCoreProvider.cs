namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class TaskApiWorkerCoreProvider : IWorkerCoreProvider<ITaskApiWorkerCore>
    {
        private readonly ILogger _logger;
        private readonly TaskApi.TaskApiClient _taskApiClient;
        private readonly string _workerDisplayName;
        private readonly AsyncEvent<IWorkerCoreProvider<ITaskApiWorkerCore>> _onTaskApiDisconnected;

        public TaskApiWorkerCoreProvider(
            ILogger logger,
            TaskApi.TaskApiClient taskApiClient,
            string workerUniqueId,
            string workerDisplayName)
        {
            _logger = logger;
            _taskApiClient = taskApiClient;
            Id = workerUniqueId;
            _workerDisplayName = workerDisplayName;
            _onTaskApiDisconnected = new AsyncEvent<IWorkerCoreProvider<ITaskApiWorkerCore>>();
        }

        public string Id { get; }

        public string DisplayName => _workerDisplayName;

        public IAsyncEvent<IWorkerCoreProvider<ITaskApiWorkerCore>> OnTaskApiDisconnected => _onTaskApiDisconnected;

        public async Task<ITaskApiWorkerCore> RequestCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Open the request to the remote worker.
                var request = _taskApiClient.ReserveCoreAndExecute(cancellationToken: cancellationToken);

                // Send the request to reserve a core.
                _logger.LogInformation($"Requesting a core from {_workerDisplayName}...");
                await request.RequestStream.WriteAsync(new ExecutionRequest
                {
                    ReserveCore = new ReserveCoreRequest
                    {
                    }
                }, cancellationToken);

                // Get the core reserved response. This operation will cease if the 
                // cancellation token is cancelled before the reservation is made.
                if (!await request.ResponseStream.MoveNext(cancellationToken))
                {
                    // The server disconnected from us without reserving a core. This
                    // can happen if the remote worker is e.g. shutting down.
                    throw new RpcException(new Status(StatusCode.Unavailable, "The remote worker ended the call without reserving a core."));
                }
                if (request.ResponseStream.Current.ResponseCase != ExecutionResponse.ResponseOneofCase.ReserveCore)
                {
                    // The server gave us a response that wasn't core reservation. This
                    // is unexpected and we have no recovery from this.
                    throw new RpcException(new Status(StatusCode.Unavailable, "The remote worker responded with a non-ReserveCore response."));
                }
                _logger.LogInformation($"Obtained a core from {_workerDisplayName}, pushing it to the queue.");

                // Get information about the reservation.
                var reservationInfo = request.ResponseStream.Current.ReserveCore;
                return new TaskApiWorkerCore(
                    _logger,
                    reservationInfo.WorkerMachineName,
                    reservationInfo.WorkerCoreNumber,
                    reservationInfo.WorkerCoreUniqueAssignmentId,
                    request);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
            {
                await _onTaskApiDisconnected.BroadcastAsync(this, CancellationToken.None);
                throw;
            }
        }

        private class TaskApiWorkerCore : ITaskApiWorkerCore
        {
            private readonly ILogger _logger;
            private bool _alive;

            public TaskApiWorkerCore(
                ILogger logger,
                string workerMachineName,
                int workerCoreNumber,
                string workerCoreUniqueAssignmentId,
                AsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> call)
            {
                _logger = logger;
                _alive = true;
                WorkerMachineName = workerMachineName;
                WorkerCoreNumber = workerCoreNumber;
                WorkerCoreUniqueAssignmentId = workerCoreUniqueAssignmentId;
                Request = new BufferedAsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse>(
                    logger,
                    call);
                Request.OnTerminated.Add(OnTerminated);
                Request.StartObserving();
            }

            public string WorkerMachineName { get; }

            public int WorkerCoreNumber { get; }

            public string WorkerCoreUniqueAssignmentId { get; }

            public BufferedAsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> Request { get; }

            private Task OnTerminated(StatusCode statusCode, CancellationToken token)
            {
                _logger.LogInformation($"Marking core as dead due to status code: {statusCode}");
                _alive = false;
                return Task.CompletedTask;
            }

            public async ValueTask DisposeAsync()
            {
                _alive = false;
                try
                {
                    // Try to cleanly close if the connection is still open.
                    await Request.RequestStream.CompleteAsync();
                }
                catch
                {
                }
                await Request.OnTerminated.RemoveAsync(OnTerminated);
            }

            public ValueTask<bool> IsAliveAsync(CancellationToken cancellationToken)
            {
                return ValueTask.FromResult(_alive);
            }
        }
    }
}
