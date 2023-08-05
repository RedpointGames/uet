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

    internal class DefaultWorkerComponent : TaskApi.TaskApiBase, IWorkerComponent
    {
        private readonly IToolManager _toolManager;
        private readonly IBlobManager _blobManager;
        private readonly IExecutionManager _executionManager;
        private readonly ILogger<DefaultWorkerComponent> _logger;
        private readonly SemaphoreSlim _reservationSemaphore = new SemaphoreSlim(
            Environment.ProcessorCount * (OperatingSystem.IsMacOS() ? 1 : 2));
        private readonly CancellationTokenSource _udpCancellationTokenSource;
        private string? _listeningExternalUrl;
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
            _udpCancellationTokenSource = new CancellationTokenSource();
        }

        public TaskApi.TaskApiBase TaskApi => this;

        public string? ListeningExternalUrl => _listeningExternalUrl;

        public async Task StartAsync(CancellationToken shutdownCancellationToken)
        {
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
                    new IPEndPoint(IPAddress.Loopback, 0),
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

            _listeningExternalUrl = app.Urls.First();

            _app = app;

            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, WorkerPortInformation.WorkerUdpBroadcastPort));
            _udpTask = Task.Run(async () =>
            {
                while (_udpCancellationTokenSource.IsCancellationRequested)
                {
                    var packet = await _udp.ReceiveAsync(_udpCancellationTokenSource.Token);
                    if (Encoding.ASCII.GetString(packet.Buffer) == "OPENGE-DISCOVER")
                    {
                        await _udp.SendAsync(
                            Encoding.ASCII.GetBytes(_listeningExternalUrl!),
                            packet.RemoteEndPoint,
                            _udpCancellationTokenSource.Token);
                    }
                }
            });
        }

        public async Task StopAsync()
        {
            _udpCancellationTokenSource.Cancel();
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

        public override async Task ReserveCoreAndExecute(
            IAsyncStreamReader<ExecutionRequest> requestStream, 
            IServerStreamWriter<ExecutionResponse> responseStream, 
            ServerCallContext context)
        {
            var connectionIdleTracker = new ConnectionIdleTracker(
                context.CancellationToken,
                5000);
            connectionIdleTracker.StartIdling();
            var didReserve = false;
            try
            {
                // Wait for the reservation to be made first.
                await HandleReservationRequestLoopAsync(requestStream, responseStream, connectionIdleTracker);
                didReserve = true;

                // Process requests after the reservation.
                while (await requestStream.MoveNext(connectionIdleTracker.CancellationToken))
                {
                    switch (requestStream.Current.RequestCase)
                    {
                        case ExecutionRequest.RequestOneofCase.ReserveCore:
                            throw new RpcException(new Status(StatusCode.InvalidArgument, "You have already obtained a reservation on this RPC call."));
                        case ExecutionRequest.RequestOneofCase.QueryTool:
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
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.HasToolBlobs:
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
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.WriteToolBlob:
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    WriteToolBlob = await _toolManager.WriteToolBlobAsync(
                                        requestStream.Current.WriteToolBlob,
                                        context.CancellationToken),
                                });
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.ConstructTool:
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
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.QueryMissingBlobs:
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    QueryMissingBlobs = await _blobManager.QueryMissingBlobsAsync(
                                        requestStream.Current.QueryMissingBlobs,
                                        context.CancellationToken),
                                });
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.SendCompressedBlobs:
                            connectionIdleTracker.StopIdling();
                            try
                            {
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    SendCompressedBlobs = await _blobManager.SendCompressedBlobsAsync(
                                        requestStream.Current.SendCompressedBlobs,
                                        context.CancellationToken),
                                });
                            }
                            finally
                            {
                                connectionIdleTracker.StartIdling();
                            }
                            break;
                        case ExecutionRequest.RequestOneofCase.ExecuteTask:
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
                            }
                            break;
                        default:
                            throw new RpcException(new Status(StatusCode.Unimplemented, "Unexpected RPC request."));
                    }
                }
            }
            finally
            {
                if (didReserve)
                {
                    _reservationSemaphore.Release();
                }
            }
        }

        private async Task HandleReservationRequestLoopAsync(
            IAsyncStreamReader<ExecutionRequest> requestStream,
            IServerStreamWriter<ExecutionResponse> responseStream,
            ConnectionIdleTracker connectionIdleTracker)
        {
            bool didReserve = false;
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
                                didReserve = true;
                                await responseStream.WriteAsync(new ExecutionResponse
                                {
                                    ReserveCore = new ReserveCoreResponse
                                    {
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
                    return;
                }
            } 
            finally
            {
                if (didReserve && !isReturning)
                {
                    _reservationSemaphore.Release();
                }
            }
        }
    }
}
