namespace Redpoint.CloudFramework.Processor
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System.Threading.Tasks;

    internal class ContinuousProcessorHostedService<T> : IHostedService, IAsyncDisposable where T : IContinuousProcessor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ContinuousProcessorHostedService<T>> _logger;

        private CancellationTokenSource? _cancellationTokenSource = null;
        private Task? _runningTask = null;

        public ContinuousProcessorHostedService(
            IServiceProvider serviceProvider,
            ILogger<ContinuousProcessorHostedService<T>> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await StopInternalAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation($"ContinuousProcessorHostedService<{typeof(T).Name}>.StartAsync: Creating new CTS.");
            var cancellationTokenSource = _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _logger.LogInformation($"ContinuousProcessorHostedService<{typeof(T).Name}>.StartAsync: Starting the running task via Task.Run.");
            _runningTask = Task.Run(() =>
            {
                var instance = _serviceProvider.GetRequiredService<T>();
                _logger.LogInformation($"ContinuousProcessorHostedService<{typeof(T).Name}>.StartAsync: Calling ExecuteAsync inside Task.Run.");
                return instance.ExecuteAsync(cancellationTokenSource.Token);
            }, cancellationTokenSource.Token);
        }

        private async Task StopInternalAsync(CancellationToken cancellationToken)
        {
            if (_cancellationTokenSource != null)
            {
                if (_runningTask != null)
                {
                    _logger.LogInformation($"ContinuousProcessorHostedService<{typeof(T).Name}>.StopInternalAsync: Cancelling CTS.");
                    _cancellationTokenSource.Cancel();
                    try
                    {
                        _logger.LogInformation($"ContinuousProcessorHostedService<{typeof(T).Name}>.StopInternalAsync: Awaiting the running task to allow it to gracefully stop...");
                        await _runningTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation($"ContinuousProcessorHostedService<{typeof(T).Name}>.StopInternalAsync: Awaited task threw OperationCanceledException (this is normal).");
                    }
                    finally
                    {
                        _logger.LogInformation($"ContinuousProcessorHostedService<{typeof(T).Name}>.StopInternalAsync: Clearing _runningTask to null.");
                        _runningTask = null;
                    }
                }
                _logger.LogInformation($"ContinuousProcessorHostedService<{typeof(T).Name}>.StopInternalAsync: Disposing CTS.");
                _cancellationTokenSource.Dispose();
                _logger.LogInformation($"ContinuousProcessorHostedService<{typeof(T).Name}>.StopInternalAsync: Clearing CTS to null.");
                _cancellationTokenSource = null;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"ContinuousProcessorHostedService<{typeof(T).Name}>.StopAsync: Deferring to StopInternalAsync.");
            await StopInternalAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation($"ContinuousProcessorHostedService<{typeof(T).Name}>.DisposeAsync: Deferring to StopAsync.");
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
