namespace Redpoint.OpenGE.Component.Worker
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using System.Net;
    using System.Collections.Concurrent;
    using Redpoint.OpenGE.Core;
    using Redpoint.AutoDiscovery;
    using Redpoint.Logging;
    using Redpoint.GrpcPipes;

    internal class DefaultWorkerComponent : TaskApi.TaskApiBase, IWorkerComponent
    {
        private readonly IToolManager _toolManager;
        private readonly IBlobManager _blobManager;
        private readonly IExecutionManager _executionManager;
        private readonly ILogger<DefaultWorkerComponent> _logger;
        private readonly INetworkAutoDiscovery _networkAutoDiscovery;
        private readonly IGrpcPipeFactory _grpcPipeFactory;
        private readonly bool _localUseOnly;
        private readonly string _workerDisplayName;
        private readonly string _workerUniqueId;
        private readonly Concurrency.Semaphore _reservationSemaphore;
        private readonly ConcurrentBag<int> _reservationBag;
        private CancellationTokenSource? _shutdownCancellationTokenSource;
        private IGrpcPipeServer<DefaultWorkerComponent>? _app;
        private int? _listeningPort;
        private IAsyncDisposable? _autoDiscoveryInstance;

        public DefaultWorkerComponent(
            IToolManager toolManager,
            IBlobManager blobManager,
            IExecutionManager executionManager,
            ILogger<DefaultWorkerComponent> logger,
            INetworkAutoDiscovery networkAutoDiscovery,
            IGrpcPipeFactory grpcPipeFactory,
            bool localUseOnly)
        {
            _toolManager = toolManager;
            _blobManager = blobManager;
            _executionManager = executionManager;
            _logger = logger;
            _networkAutoDiscovery = networkAutoDiscovery;
            _grpcPipeFactory = grpcPipeFactory;
            _localUseOnly = localUseOnly;
            _workerDisplayName = Environment.MachineName;
            _workerUniqueId = Guid.NewGuid().ToString();

            var processorCount = Environment.ProcessorCount;// * (OperatingSystem.IsMacOS() ? 1 : 2);
            _reservationSemaphore = new Concurrency.Semaphore(processorCount);
            _reservationBag = new ConcurrentBag<int>(Enumerable.Range(1, processorCount));
        }

        public TaskApi.TaskApiBase TaskApi => this;

        public int? ListeningPort => _listeningPort;

        public string WorkerDisplayName => _workerDisplayName;

        public string WorkerUniqueId => _workerUniqueId;

        public async Task StartAsync(CancellationToken shutdownCancellationToken)
        {
            _shutdownCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownCancellationToken);

            _app = _grpcPipeFactory.CreateNetworkServer(this, _localUseOnly);
            await _app.StartAsync().ConfigureAwait(false);

            _listeningPort = _app.NetworkPort;
            _logger.LogInformation($"Worker listening on port: {_listeningPort}");

            if (!_localUseOnly)
            {
                var autoDiscoveryName = $"{_workerUniqueId}._{WorkerDiscoveryConstants.OpenGEPlatformIdentifier}{WorkerDiscoveryConstants.OpenGEProtocolVersion}-openge._tcp.local";
                _autoDiscoveryInstance = await _networkAutoDiscovery.RegisterServiceAsync(
                    autoDiscoveryName,
                    _listeningPort.Value,
                    shutdownCancellationToken).ConfigureAwait(false);
                _logger.LogInformation($"Worker advertising on auto-discovery name: {autoDiscoveryName}");
            }
        }

        public async Task StopAsync()
        {
            _shutdownCancellationTokenSource!.Cancel();
            if (_autoDiscoveryInstance != null)
            {
                await _autoDiscoveryInstance.DisposeAsync().ConfigureAwait(false);
                _autoDiscoveryInstance = null;
            }
            if (_app != null)
            {
                await _app.StopAsync().ConfigureAwait(false);
                _app = null;
            }
        }

        private static async Task<bool> PullFromRequestStreamAsync(
            IAsyncStreamReader<ExecutionRequest> requestStream,
            CancellationToken cancellationToken)
        {
            try
            {
                return await requestStream.MoveNext(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // We closed the stream.
                return false;
            }
        }

        private class WorkerRequestStream : IWorkerRequestStream
        {
            private readonly IAsyncStreamReader<ExecutionRequest> _requestStream;

            public WorkerRequestStream(IAsyncStreamReader<ExecutionRequest> requestStream)
            {
                _requestStream = requestStream;
            }

            public Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                return PullFromRequestStreamAsync(
                    _requestStream,
                    cancellationToken);
            }

            public ExecutionRequest Current => _requestStream.Current;
        }

        public override Task<PingTaskServiceResponse> PingTaskService(PingTaskServiceRequest request, ServerCallContext context)
        {
            return Task.FromResult(new PingTaskServiceResponse());
        }

        public override async Task ReserveCoreAndExecute(
            IAsyncStreamReader<ExecutionRequest> requestStream,
            IServerStreamWriter<ExecutionResponse> responseStream,
            ServerCallContext context)
        {
            var uniqueAssignmentId = Guid.NewGuid().ToString();
            var connectionIdleTracker = new ConnectionIdleTracker(
                CancellationTokenSource.CreateLinkedTokenSource(
                    context.CancellationToken,
                    _shutdownCancellationTokenSource!.Token).Token,
                5000,
                () =>
                {
                    if (OperatingSystem.IsMacOS())
                    {
                        // @note: It looks like there might be a macOS-specific bug where a server cancelling a streaming RPC
                        // request causes all other streaming RPC calls to stall indefinitely. See the GitHub issue for
                        // more information: https://github.com/grpc/grpc-dotnet/issues/2280
                        return ConnectionIdleEventOutcome.ResetIdleTimer;
                    }
                    else if (_blobManager.IsTransferringFromPeer(context))
                    {
                        return ConnectionIdleEventOutcome.ResetIdleTimer;
                    }
                    else
                    {
                        return ConnectionIdleEventOutcome.Idled;
                    }
                });
            connectionIdleTracker.StartIdling();
            var didReserve = false;
            int reservedCore = 0;
            ExecutionRequest.RequestOneofCase lastRequest = ExecutionRequest.RequestOneofCase.None;
            DateTimeOffset lastRequestTime = DateTimeOffset.UtcNow;
            try
            {
                // Wait for the reservation to be made first.
                reservedCore = await HandleReservationRequestLoopAsync(requestStream, responseStream, uniqueAssignmentId, connectionIdleTracker).ConfigureAwait(false);
                didReserve = true;

                // Process requests after the reservation.
                while (await PullFromRequestStreamAsync(requestStream, connectionIdleTracker.CancellationToken).ConfigureAwait(false))
                {
                    lastRequest = requestStream.Current.RequestCase;
                    switch (requestStream.Current.RequestCase)
                    {
                        case ExecutionRequest.RequestOneofCase.ReserveCore:
                            throw new RpcException(new Status(StatusCode.InvalidArgument, "You have already obtained a reservation on this RPC call."));
                        case ExecutionRequest.RequestOneofCase.QueryTool:
                            _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute QueryTool Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    QueryTool = await _toolManager.QueryToolAsync(
                                        requestStream.Current.QueryTool,
                                        context.CancellationToken).ConfigureAwait(false),
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute QueryTool End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.HasToolBlobs:
                            _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute HasToolBlobs Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    HasToolBlobs = await _toolManager.HasToolBlobsAsync(
                                        requestStream.Current.HasToolBlobs,
                                        context.CancellationToken).ConfigureAwait(false),
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute HasToolBlobs End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.WriteToolBlob:
                            _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute WriteToolBlob Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    WriteToolBlob = await _toolManager.WriteToolBlobAsync(
                                        requestStream.Current.WriteToolBlob,
                                        new WorkerRequestStream(requestStream),
                                        context.CancellationToken).ConfigureAwait(false),
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute WriteToolBlob End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.ConstructTool:
                            _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute ConstructTool Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    ConstructTool = await _toolManager.ConstructToolAsync(
                                        requestStream.Current.ConstructTool,
                                        context.CancellationToken).ConfigureAwait(false),
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute ConstructTool End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.QueryMissingBlobs:
                            _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute QueryMissingBlobs Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await _blobManager.QueryMissingBlobsAsync(
                                    context,
                                    requestStream.Current.QueryMissingBlobs,
                                    responseStream,
                                    context.CancellationToken).ConfigureAwait(false);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute QueryMissingBlobs End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.SendCompressedBlobs:
                            _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute SendCompressedBlobs Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await _blobManager.SendCompressedBlobsAsync(
                                    context,
                                    requestStream.Current,
                                    requestStream,
                                    responseStream,
                                    context.CancellationToken).ConfigureAwait(false);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute SendCompressedBlobs End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.ExecuteTask:
                            _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute ExecuteTask Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await _executionManager.ExecuteTaskAsync(
                                    GrpcPeerParser.ParsePeer(context).Address,
                                    requestStream.Current.ExecuteTask,
                                    responseStream,
                                    context.CancellationToken).ConfigureAwait(false);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute ExecuteTask End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.ReceiveOutputBlobs:
                            _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute ReceiveOutputBlobs Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await _blobManager.ReceiveOutputBlobsAsync(
                                    context,
                                    requestStream.Current,
                                    responseStream,
                                    context.CancellationToken).ConfigureAwait(false);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace($"{uniqueAssignmentId}: Worker ReserveCoreAndExecute ReceiveOutputBlobs End");
                            }
                            break;
                        default:
                            throw new RpcException(new Status(StatusCode.Unimplemented, $"Worker does not know how to handle this RPC request: '{requestStream.Current.RequestCase}'"));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    if (didReserve)
                    {
                        _logger.LogTrace($"{uniqueAssignmentId}: The caller of ReserveCoreAndExecute RPC cancelled the operation, so the reservation was released.");
                    }
                    return;
                }
                else if (_shutdownCancellationTokenSource!.IsCancellationRequested)
                {
                    _logger.LogTrace($"{uniqueAssignmentId}: The reservation was cancelled because the worker is shutting down.");
                    throw new RpcException(new Status(StatusCode.Cancelled, "Worker is shutting down, so the reservation was released."));
                }
                else if (connectionIdleTracker.CancellationToken.IsCancellationRequested)
                {
                    // @todo: This maybe should still be a warning, but we need to make sure it doesn't get emitted for in-process.
                    _logger.LogTrace($"{uniqueAssignmentId}: The entity calling ReserveCoreAndExecute RPC idled for too long, and the call was cancelled because the reservation was not being used. The last request was {lastRequest} approximately {(DateTimeOffset.UtcNow - lastRequestTime).TotalSeconds} seconds ago.");
                    throw new RpcException(new Status(StatusCode.Cancelled, "Connection idled for too long, so the reservation was released."));
                }
            }
            finally
            {
                _blobManager.NotifyServerCallEnded(context);

                if (didReserve)
                {
                    _reservationBag.Add(reservedCore);
                    _reservationSemaphore.Release();
                }
            }
        }

        private async Task<int> HandleReservationRequestLoopAsync(
            IAsyncStreamReader<ExecutionRequest> requestStream,
            IServerStreamWriter<ExecutionResponse> responseStream,
            string uniqueAssignmentId,
            ConnectionIdleTracker connectionIdleTracker)
        {
            bool didReserve = false;
            int reservedCore = 0;
            bool isReturning = false;
            try
            {
                var shouldReturn = false;
                while (!shouldReturn &&
                    await requestStream.MoveNext(connectionIdleTracker.CancellationToken).ConfigureAwait(false))
                {
                    switch (requestStream.Current.RequestCase)
                    {
                        case ExecutionRequest.RequestOneofCase.ReserveCore:
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await _reservationSemaphore.WaitAsync(connectionIdleTracker.CancellationToken).ConfigureAwait(false);
                                if (!_reservationBag.TryTake(out reservedCore))
                                {
                                    throw new RpcException(new Status(
                                        StatusCode.InvalidArgument,
                                        "Core reservation bag has less items than semaphore!"));
                                }
                                didReserve = true;
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    ReserveCore = new ReserveCoreResponse
                                    {
                                        WorkerMachineName = Environment.MachineName,
                                        WorkerCoreNumber = reservedCore,
                                        WorkerCoreUniqueAssignmentId = uniqueAssignmentId,
                                    }
                                }).ConfigureAwait(false);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                            }
                            shouldReturn = true;
                            break;
                        default:
                            throw new RpcException(new Status(
                                StatusCode.InvalidArgument,
                                "You must use ReserveCode before making any other requests."));
                    }
                }
                if (shouldReturn)
                {
                    // @note: This sequence of returns seems like jank, but it
                    // guarantees that either:
                    // - This function exits successfully with a reservation, or
                    // - This function exits without ever taking the reservation, or
                    // - This function exits with an exception, but releases the
                    //   reservation it took.
                    isReturning = true;
                    return reservedCore;
                }
            }
            finally
            {
                if (didReserve && !isReturning)
                {
                    _reservationSemaphore.Release();
                }
            }
            throw new RpcException(new Status(
                StatusCode.Internal,
                "Expected HandleReservationRequestLoopAsync to exit normally or to already have thrown."));
        }
    }
}
