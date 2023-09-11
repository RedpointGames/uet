namespace Redpoint.Uefs.Daemon
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using System.Threading;
    using static Redpoint.Uefs.Protocol.Uefs;
    using Grpc.Core;

    internal sealed class UefsHealthCheckService : IHostedService
    {
        private readonly ILogger<UefsHealthCheckService> _logger;
        private readonly IGrpcPipeFactory _grpcPipeFactory;
        private readonly CancellationTokenSource _cts;
        private Task? _healthCheckTask;

        public UefsHealthCheckService(
            ILogger<UefsHealthCheckService> logger,
            IGrpcPipeFactory grpcPipeFactory)
        {
            _logger = logger;
            _grpcPipeFactory = grpcPipeFactory;
            _cts = new CancellationTokenSource();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _healthCheckTask = Task.Run(HealthCheckAsync);
            return Task.CompletedTask;
        }

        private async Task HealthCheckAsync()
        {
            _logger.LogInformation("gRPC self health check is now running...");
            var failures = 0;
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var client = _grpcPipeFactory.CreateClient(
                        "UEFS",
                        GrpcPipeNamespace.Computer,
                        channel => new UefsClient(channel));
                    await client.PingAsync(new Protocol.PingRequest(), deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: _cts.Token);
                    failures = 0;
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    // Health check is intentionally shutting down.
                    return;
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
                {
                    if (failures < 3)
                    {
                        failures++;
                        _logger.LogWarning($"gRPC client is not responding for health check (failure {failures} of 3)...");
                    }
                    else
                    {
                        _logger.LogError($"Detected gRPC client is no longer usable! Forcing UEFS to exit!");
                        Environment.Exit(1);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, $"Unexpected exception during gRPC health check: {ex}");
                }

                await Task.Delay(1000, _cts.Token);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_healthCheckTask != null)
            {
                _cts.Cancel();
                try
                {
                    await _healthCheckTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }
}

