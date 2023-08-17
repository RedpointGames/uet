namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Google.Protobuf;
    using Grpc.Core;
    using Grpc.Net.Client;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultWorkerPool : IWorkerPool
    {
        private readonly ILogger<DefaultWorkerPool> _logger;
        private readonly SemaphoreSlim _notifyReevaluationOfWorkers;
        private readonly CancellationTokenSource _disposedCts;
        internal readonly WorkerSubpool _localSubpool;
        internal readonly WorkerSubpool _remoteSubpool;
        internal readonly ConcurrentDictionary<string, bool> _remoteWorkersHandled;

        private readonly Task _workersProcessingTask;
        private readonly Task _discoverRemoteWorkersTask;

        public DefaultWorkerPool(
            ILogger<DefaultWorkerPool> logger,
            ILogger<WorkerSubpool> subpoolLogger,
            WorkerAddRequest? localWorkerAddRequest)
        {
            _logger = logger;
            _notifyReevaluationOfWorkers = new SemaphoreSlim(0);
            _disposedCts = new CancellationTokenSource();
            _localSubpool = new WorkerSubpool(
                subpoolLogger,
                _notifyReevaluationOfWorkers);
            _remoteSubpool = new WorkerSubpool(
                subpoolLogger,
                _notifyReevaluationOfWorkers);
            _remoteWorkersHandled = new ConcurrentDictionary<string, bool>();

            if (localWorkerAddRequest != null)
            {
                _localSubpool._workers.Add(new WorkerState
                {
                    DisplayName = localWorkerAddRequest.DisplayName,
                    Client = localWorkerAddRequest.Client,
                    UniqueId = localWorkerAddRequest.UniqueId,
                    IsLocalWorker = true,
                });
                _remoteSubpool._workers.Add(new WorkerState
                {
                    DisplayName = localWorkerAddRequest.DisplayName,
                    Client = localWorkerAddRequest.Client,
                    UniqueId = localWorkerAddRequest.UniqueId,
                    IsLocalWorker = true,
                });
            }

            _workersProcessingTask = Task.Run(PeriodicallyProcessWorkers);
            _discoverRemoteWorkersTask = Task.Run(DiscoverRemoteWorkers);
        }

        private async Task DiscoverRemoteWorkers()
        {
            var request = new WorkerDiscoveryRequest
            {
                OpengeMagicNumber = WorkerDiscoveryConstants.OpenGEMagicNumber,
                OpengeProtocolVersion = WorkerDiscoveryConstants.OpenGEProtocolVersion,
                WorkerPlatform = true switch
                {
                    var v when v == OperatingSystem.IsWindows() => WorkerPlatform.Windows,
                    var v when v == OperatingSystem.IsMacOS() => WorkerPlatform.Mac,
                    var v when v == OperatingSystem.IsLinux() => WorkerPlatform.Linux,
                    _ => WorkerPlatform.Unknown,
                }
            };

            var client = new UdpClient();
            client.EnableBroadcast = true;
            client.Client.Bind(new IPEndPoint(
                IPAddress.Parse("10.7.0.241"),
                0));

            int sendDelaySeconds = 5;

            var sendLoop = Task.Run(async () =>
            {
                while (!_disposedCts.IsCancellationRequested)
                {
                    _logger.LogInformation("Attempting to discover other workers on the local network...");
                    for (int i = 0; i < 10; i++)
                    {
                        await client.SendAsync(
                            request.ToByteArray(),
                            new IPEndPoint(
                                IPAddress.Parse("10.7.0.25"), 
                                WorkerDiscoveryConstants.WorkerUdpBroadcastPort),
                            _disposedCts.Token);
                    }

                    await Task.Delay(sendDelaySeconds * 1000, _disposedCts.Token);
                    sendDelaySeconds *= 2;
                    if (sendDelaySeconds > 120)
                    {
                        sendDelaySeconds = 120;
                    }
                }
            });

            var receiveLoop = Task.Run(async () =>
            {
                while (!_disposedCts.IsCancellationRequested)
                {
                    var packet = await client.ReceiveAsync(_disposedCts.Token);
                    try
                    {
                        _logger.LogInformation($"Received packet from: {packet.RemoteEndPoint}");
                        var response = WorkerDiscoveryResponse.Parser.ParseFrom(packet.Buffer);

                        // Try to ping this remote worker.
                        var taskApi = new TaskApi.TaskApiClient(
                            GrpcChannel.ForAddress($"http://{packet.RemoteEndPoint.Address}:{response.WorkerPort}"));
                        var usable = false;
                        try
                        {
                            await taskApi.PingTaskServiceAsync(
                                new PingTaskServiceRequest(),
                                deadline: DateTime.UtcNow.AddSeconds(5));
                            usable = true;
                        }
                        catch
                        {
                        }
                        if (usable)
                        {
                            if (!_remoteWorkersHandled.TryGetValue(response.WorkerUniqueId, out var exists))
                            {
                                // If we got a worker that we've never seen before, broadcast immediately
                                // from us so that we discover them in return.
                                await client.SendAsync(
                                    request.ToByteArray(),
                                    new IPEndPoint(IPAddress.Broadcast, WorkerDiscoveryConstants.WorkerUdpBroadcastPort),
                                    _disposedCts.Token);
                                _remoteWorkersHandled.TryAdd(response.WorkerUniqueId, true);
                            }

                            await _remoteSubpool.RegisterWorkerAsync(new WorkerAddRequest
                            {
                                DisplayName = response.WorkerDisplayName,
                                UniqueId = response.WorkerUniqueId,
                                Client = taskApi,
                            });
                            _logger.LogInformation($"Discovered remote worker {response.WorkerDisplayName} at '{packet.RemoteEndPoint}'.");
                        }
                        else
                        {
                            _logger.LogWarning($"Discovered remote worker at {packet.RemoteEndPoint.Address}, but it wasn't usable based on the gRPC ping request.");
                        }
                    }
                    catch
                    {
                        _logger.LogWarning($"Ignoring unknown discovery response packet from {packet.RemoteEndPoint}");
                    }
                }
            });

            await Task.WhenAll(sendLoop, receiveLoop);
        }

        private async Task PeriodicallyProcessWorkers()
        {
            while (!_disposedCts.IsCancellationRequested)
            {
                // Reprocess remote workers state either:
                // - Every 10 seconds, or
                // - When the notification semaphore tells us we need to reprocess now.
                var timingCts = CancellationTokenSource.CreateLinkedTokenSource(
                    new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token,
                    _disposedCts.Token);
                try
                {
                    await _notifyReevaluationOfWorkers.WaitAsync(timingCts.Token);
                }
                catch (OperationCanceledException) when (timingCts.IsCancellationRequested)
                {
                }
                if (_disposedCts.IsCancellationRequested)
                {
                    // The worker pool is disposing.
                    return;
                }

                await _localSubpool.ProcessWorkersAsync();
                await _remoteSubpool.ProcessWorkersAsync();
            }
        }

        public Task<IWorkerCore> ReserveCoreAsync(
            bool requireLocalCore,
            CancellationToken cancellationToken)
        {
            if (requireLocalCore)
            {
                return _localSubpool.ReserveCoreAsync(cancellationToken);
            }
            else
            {
                return _remoteSubpool.ReserveCoreAsync(cancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _disposedCts.Cancel();
            try
            {
                await _workersProcessingTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
