namespace Redpoint.OpenGE.Component.Worker
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Collections.Concurrent;
    using Google.Protobuf;
    using Redpoint.OpenGE.Core;

    internal class DefaultWorkerComponent : TaskApi.TaskApiBase, IWorkerComponent
    {
        private readonly IToolManager _toolManager;
        private readonly IBlobManager _blobManager;
        private readonly IExecutionManager _executionManager;
        private readonly ILogger<DefaultWorkerComponent> _logger;
        private readonly string _workerDisplayName;
        private readonly string _workerUniqueId;
        private readonly SemaphoreSlim _reservationSemaphore;
        private readonly ConcurrentBag<int> _reservationBag;
        private CancellationTokenSource? _shutdownCancellationTokenSource;
        private int? _listeningPort;
        private WebApplication? _app;
        private UdpClient? _udp;
        private Task? _udpTask;

        public DefaultWorkerComponent(
            IToolManager toolManager,
            IBlobManager blobManager,
            IExecutionManager executionManager,
            ILogger<DefaultWorkerComponent> logger)
        {
            _toolManager = toolManager;
            _blobManager = blobManager;
            _executionManager = executionManager;
            _logger = logger;

            _workerDisplayName = Environment.MachineName;
            _workerUniqueId = Guid.NewGuid().ToString();

            var processorCount = Environment.ProcessorCount * (OperatingSystem.IsMacOS() ? 1 : 2);
            _reservationSemaphore = new SemaphoreSlim(processorCount);
            _reservationBag = new ConcurrentBag<int>(Enumerable.Range(1, processorCount));
        }

        public TaskApi.TaskApiBase TaskApi => this;

        public int? ListeningPort => _listeningPort;

        public string WorkerDisplayName => _workerDisplayName;

        public string WorkerUniqueId => _workerUniqueId;

        public async Task StartAsync(CancellationToken shutdownCancellationToken)
        {
            _shutdownCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownCancellationToken);

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new ForwardingLoggerProvider(_logger));
            builder.Services.AddGrpc(options =>
            {
                // Allow unlimited message sizes.
                options.MaxReceiveMessageSize = null;
                options.MaxSendMessageSize = null;
            });
            builder.Services.Add(new ServiceDescriptor(
                typeof(TaskApi.TaskApiBase),
                this));
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.Listen(
                    new IPEndPoint(IPAddress.Any, 0),
                    listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
            });

            var app = builder.Build();
            app.UseRouting();
            app.UseGrpcWeb();
            app.MapGrpcService<TaskApi.TaskApiBase>();

            await app.StartAsync();

            _listeningPort = new Uri(app.Urls.First()).Port;

            _logger.LogInformation($"Worker listening on port: {_listeningPort}");

            _app = app;

            try
            {
                _udp = new UdpClient();
                _udp.Client.Bind(new IPEndPoint(IPAddress.Parse("10.7.0.241"), WorkerDiscoveryConstants.WorkerUdpBroadcastPort));
                _udpTask = Task.Run(async () =>
                {
                    while (_shutdownCancellationTokenSource.IsCancellationRequested)
                    {
                        var packet = await _udp.ReceiveAsync(_shutdownCancellationTokenSource.Token);
                        _logger.LogInformation($"Got incoming packet on auto-discovery port from remote: {packet.RemoteEndPoint}");
                        try
                        {
                            var request = WorkerDiscoveryRequest.Parser.ParseFrom(packet.Buffer);
                            var currentPlatform = true switch
                            {
                                var v when v == OperatingSystem.IsWindows() => WorkerPlatform.Windows,
                                var v when v == OperatingSystem.IsMacOS() => WorkerPlatform.Mac,
                                var v when v == OperatingSystem.IsLinux() => WorkerPlatform.Linux,
                                _ => WorkerPlatform.Unknown,
                            };
                            if (request.OpengeMagicNumber == WorkerDiscoveryConstants.OpenGEMagicNumber &&
                                request.OpengeProtocolVersion == WorkerDiscoveryConstants.OpenGEProtocolVersion &&
                                request.WorkerPlatform == currentPlatform)
                            {
                                var response = new WorkerDiscoveryResponse
                                {
                                    WorkerPort = _listeningPort.Value,
                                    WorkerDisplayName = _workerDisplayName,
                                    WorkerUniqueId = _workerUniqueId,
                                }.ToByteArray();
                                await _udp.SendAsync(
                                    response,
                                    packet.RemoteEndPoint,
                                    _shutdownCancellationTokenSource.Token);
                            }
                            else
                            {
                                _logger.LogInformation($"Incoming request didn't match constraints: {request}");
                            }
                        }
                        catch
                        {
                            _logger.LogWarning($"Ignoring unknown discovery packet from {packet.RemoteEndPoint}");
                        }
                    }
                });
                _logger.LogInformation($"Worker discovery listening on UDP port: {WorkerDiscoveryConstants.WorkerUdpBroadcastPort}");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                // There's already an instance running.
                _logger.LogInformation($"Worker discovery turned off as there is already a worker listening on UDP port: {WorkerDiscoveryConstants.WorkerUdpBroadcastPort}");
            }
        }

        public async Task StopAsync()
        {
            _shutdownCancellationTokenSource!.Cancel();
            if (_udp != null)
            {
                _udp.Close();
                _udp.Dispose();
            }
            if (_app != null)
            {
                await _app.StopAsync();
                _app = null;
            }
        }

        private static async Task<bool> PullFromRequestStreamAsync(
            IAsyncStreamReader<ExecutionRequest> requestStream,
            CancellationToken cancellationToken)
        {
            try
            {
                return await requestStream.MoveNext(cancellationToken);
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
            var connectionIdleTracker = new ConnectionIdleTracker(
                CancellationTokenSource.CreateLinkedTokenSource(
                    context.CancellationToken, 
                    _shutdownCancellationTokenSource!.Token).Token,
                5000);
            connectionIdleTracker.StartIdling();
            var didReserve = false;
            int reservedCore = 0;
            try
            {
                // Wait for the reservation to be made first.
                reservedCore = await HandleReservationRequestLoopAsync(requestStream, responseStream, connectionIdleTracker);
                didReserve = true;

                // Process requests after the reservation.
                while (await PullFromRequestStreamAsync(requestStream, connectionIdleTracker.CancellationToken))
                {
                    switch (requestStream.Current.RequestCase)
                    {
                        case ExecutionRequest.RequestOneofCase.ReserveCore:
                            throw new RpcException(new Status(StatusCode.InvalidArgument, "You have already obtained a reservation on this RPC call."));
                        case ExecutionRequest.RequestOneofCase.QueryTool:
                            _logger.LogTrace("Worker ReserveCoreAndExecute QueryTool Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    QueryTool = await _toolManager.QueryToolAsync(
                                        requestStream.Current.QueryTool, 
                                        context.CancellationToken),
                                });
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace("Worker ReserveCoreAndExecute QueryTool End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.HasToolBlobs:
                            _logger.LogTrace("Worker ReserveCoreAndExecute HasToolBlobs Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    HasToolBlobs = await _toolManager.HasToolBlobsAsync(
                                        requestStream.Current.HasToolBlobs,
                                        context.CancellationToken),
                                });
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace("Worker ReserveCoreAndExecute HasToolBlobs End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.WriteToolBlob:
                            _logger.LogTrace("Worker ReserveCoreAndExecute WriteToolBlob Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    WriteToolBlob = await _toolManager.WriteToolBlobAsync(
                                        requestStream.Current.WriteToolBlob,
                                        new WorkerRequestStream(requestStream),
                                        context.CancellationToken),
                                });
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace("Worker ReserveCoreAndExecute WriteToolBlob End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.ConstructTool:
                            _logger.LogTrace("Worker ReserveCoreAndExecute ConstructTool Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    ConstructTool = await _toolManager.ConstructToolAsync(
                                        requestStream.Current.ConstructTool,
                                        context.CancellationToken),
                                });
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace("Worker ReserveCoreAndExecute ConstructTool End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.QueryMissingBlobs:
                            _logger.LogTrace("Worker ReserveCoreAndExecute QueryMissingBlobs Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await _blobManager.QueryMissingBlobsAsync(
                                    context,
                                    requestStream.Current.QueryMissingBlobs,
                                    responseStream,
                                    context.CancellationToken);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace("Worker ReserveCoreAndExecute QueryMissingBlobs End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.SendCompressedBlobs:
                            _logger.LogTrace("Worker ReserveCoreAndExecute SendCompressedBlobs Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await _blobManager.SendCompressedBlobsAsync(
                                    context,
                                    requestStream.Current,
                                    requestStream,
                                    responseStream,
                                    context.CancellationToken);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace("Worker ReserveCoreAndExecute SendCompressedBlobs End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.ExecuteTask:
                            _logger.LogTrace("Worker ReserveCoreAndExecute ExecuteTask Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await _executionManager.ExecuteTaskAsync(
                                    requestStream.Current.ExecuteTask,
                                    responseStream,
                                    context.CancellationToken);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace("Worker ReserveCoreAndExecute ExecuteTask End");
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.ReceiveOutputBlobs:
                            _logger.LogTrace("Worker ReserveCoreAndExecute ReceiveOutputBlobs Begin");
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await _blobManager.ReceiveOutputBlobsAsync(
                                    context,
                                    requestStream.Current,
                                    responseStream,
                                    context.CancellationToken);
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                                _logger.LogTrace("Worker ReserveCoreAndExecute ReceiveOutputBlobs End");
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
                        _logger.LogTrace("The caller of ReserveCoreAndExecute RPC cancelled the operation, so the reservation was released.");
                    }
                    return;
                }
                else if (connectionIdleTracker.CancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("The entity calling ReserveCoreAndExecute RPC idled for too long, and the call was cancelled because the reservation was not being used.");
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
            ConnectionIdleTracker connectionIdleTracker)
        {
            bool didReserve = false;
            int reservedCore = 0;
            bool isReturning = false;
            try
            {
                var shouldReturn = false;
                while (!shouldReturn && 
                    await requestStream.MoveNext(connectionIdleTracker.CancellationToken))
                {
                    switch (requestStream.Current.RequestCase)
                    {
                        case ExecutionRequest.RequestOneofCase.ReserveCore:
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await _reservationSemaphore.WaitAsync(connectionIdleTracker.CancellationToken);
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
                                    }
                                });
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
