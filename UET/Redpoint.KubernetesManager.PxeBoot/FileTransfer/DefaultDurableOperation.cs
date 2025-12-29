namespace Redpoint.KubernetesManager.PxeBoot.FileTransfer
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using System;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using static Redpoint.KubernetesManager.PxeBoot.Client.PxeBootProvisionClientCommandInstance;

    internal class DefaultDurableOperation : IDurableOperation
    {
        private readonly ILogger<DefaultDurableOperation> _logger;

        public DefaultDurableOperation(
            ILogger<DefaultDurableOperation> logger)
        {
            _logger = logger;
        }

        public async Task DurableOperationAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
        {
        retry:
            try
            {
                await operation(cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout while running operation, retrying in 1 seconds...");
                await Task.Delay(1000, cancellationToken);
                goto retry;
            }
            catch (HttpRequestException ex) when (ex.InnerException is SocketException socketEx)
            {
                _logger.LogWarning($"Socket exception '{socketEx.Message}' while running durable operation, retrying in 1 seconds...");
                await Task.Delay(1000, cancellationToken);
                goto retry;
            }
            catch (StreamStalledException)
            {
                _logger.LogWarning($"Stream stalled while running durable operation, retrying in 1 seconds...");
                await Task.Delay(1000, cancellationToken);
                goto retry;
            }
            catch (DownloadedFileHashInvalidException)
            {
                _logger.LogWarning($"Downloaded file does not match hash in header, retrying in 1 seconds...");
                await Task.Delay(1000, cancellationToken);
                goto retry;
            }
        }

        public async Task<TOut> DurableOperationAsync<TOut>(Func<CancellationToken, Task<TOut>> operation, CancellationToken cancellationToken)
        {
        retry:
            try
            {
                return await operation(cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout while running operation, retrying in 1 seconds...");
                await Task.Delay(1000, cancellationToken);
                goto retry;
            }
            catch (HttpRequestException ex) when (ex.InnerException is SocketException socketEx)
            {
                _logger.LogWarning($"Socket exception '{socketEx.Message}' while running durable operation, retrying in 1 seconds...");
                await Task.Delay(1000, cancellationToken);
                goto retry;
            }
            catch (StreamStalledException)
            {
                _logger.LogWarning($"Stream stalled while running durable operation, retrying in 1 seconds...");
                await Task.Delay(1000, cancellationToken);
                goto retry;
            }
            catch (DownloadedFileHashInvalidException)
            {
                _logger.LogWarning($"Downloaded file does not match hash in header, retrying in 1 seconds...");
                await Task.Delay(1000, cancellationToken);
                goto retry;
            }
        }
    }
}
