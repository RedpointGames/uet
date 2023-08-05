namespace Redpoint.OpenGE.Component.Dispatcher.Tests
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;

    internal class ReservationTestingTaskServer : TaskApi.TaskApiBase
    {
        private readonly SemaphoreSlim _reservationSemaphore;
        private readonly int _idlingTimeoutMs;
        private long _reserved;

        public ReservationTestingTaskServer(int idlingTimeoutMs, int coreReservations)
        {
            _reservationSemaphore = new SemaphoreSlim(coreReservations);
            _idlingTimeoutMs = idlingTimeoutMs;
        }

        public long Reserved => _reserved;

        public override async Task ReserveCoreAndExecute(
            IAsyncStreamReader<ExecutionRequest> requestStream,
            IServerStreamWriter<ExecutionResponse> responseStream,
            ServerCallContext context)
        {
            var didReserve = false;
            var idledTooLong = new CancellationTokenSource();
            var waitingForRequest = CancellationTokenSource.CreateLinkedTokenSource(
                context.CancellationToken,
                idledTooLong.Token);
            var cancelIdling = new CancellationTokenSource();
            Task? idleCheckingTask = null;
            void StartIdleChecks()
            {
                if (idleCheckingTask != null &&
                    !idleCheckingTask.IsCompleted)
                {
                    return;
                }
                var cancelIdlingToken = cancelIdling!.Token;
                idleCheckingTask = Task.Run(async () =>
                {
                    await Task.Delay(_idlingTimeoutMs, cancelIdlingToken);
                    idledTooLong!.Cancel();
                });
            }
            void CeaseIdleChecks()
            {
                if (idleCheckingTask == null)
                {
                    return;
                }
                cancelIdling!.Cancel();
                cancelIdling = new CancellationTokenSource();
            }
            try
            {
                while (await requestStream.MoveNext(waitingForRequest.Token))
                {
                    switch (requestStream.Current.RequestCase)
                    {
                        case ExecutionRequest.RequestOneofCase.ReserveCore:
                            await _reservationSemaphore.WaitAsync(waitingForRequest.Token);
                            Interlocked.Increment(ref _reserved);
                            didReserve = true;
                            await responseStream.WriteAsync(new ExecutionResponse
                            {
                                ReserveCore = new ReserveCoreResponse
                                {
                                }
                            });
                            StartIdleChecks();
                            break;
                        case ExecutionRequest.RequestOneofCase.QueryTool:
                        case ExecutionRequest.RequestOneofCase.HasToolBlobs:
                        case ExecutionRequest.RequestOneofCase.WriteToolBlob:
                        case ExecutionRequest.RequestOneofCase.ConstructTool:
                        case ExecutionRequest.RequestOneofCase.QueryMissingBlobs:
                        case ExecutionRequest.RequestOneofCase.SendCompressedBlobs:
                        case ExecutionRequest.RequestOneofCase.ExecuteTask:
                            CeaseIdleChecks();
                            try
                            {
                                // @note: In a real server, this would be implemented.
                                throw new NotImplementedException();
                            }
                            finally
                            {
                                StartIdleChecks();
                            }
                    }
                }
            }
            finally
            {
                if (didReserve)
                {
                    Interlocked.Decrement(ref _reserved);
                    _reservationSemaphore.Release();
                }
            }
        }
    }
}