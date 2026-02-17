namespace Redpoint.CloudFramework.Processor
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ContinuousProcessorHostedService<T> : IHostedService, IAsyncDisposable where T : IContinuousProcessor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ContinuousProcessorHostedService<T>> _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        private CancellationTokenSource? _cancellationTokenSource = null;
        private Task? _runningTask = null;

        private static readonly string _typeName = $"ContinuousProcessorHostedService<{typeof(T).Name}>";

        public ContinuousProcessorHostedService(
            IServiceProvider serviceProvider,
            ILogger<ContinuousProcessorHostedService<T>> logger,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await StopInternalAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation($"{_typeName}.StartAsync: Creating new CTS.");
            var cancellationTokenSource = _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _logger.LogInformation($"{_typeName}.StartAsync: Starting the running task via Task.Run.");
            _runningTask = Task.Run(async () =>
            {
                var retryDelaySeconds = 1;
                while (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested &&
                       !_hostApplicationLifetime.ApplicationStopped.IsCancellationRequested &&
                       !cancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        await using var scope = _serviceProvider.CreateAsyncScope();
                        var instance = scope.ServiceProvider.GetRequiredService<T>();
                        _logger.LogInformation($"{_typeName}.StartAsync: Calling ExecuteAsync inside Task.Run.");
                        await instance.ExecuteAsync(cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException) when
                        (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested ||
                         _hostApplicationLifetime.ApplicationStopped.IsCancellationRequested ||
                         cancellationTokenSource.IsCancellationRequested)
                    {
                        // Application is shutting down.
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"{_typeName}.StartAsync: Unhandled exception in continuous processor.");
                        retryDelaySeconds *= 2;
                        if (retryDelaySeconds > 30 * 60)
                        {
                            // 30 minutes is long enough.
                            retryDelaySeconds = 30 * 60;
                        }

                        _logger.LogInformation($"{_typeName}.StartAsync: Restarting failed continuous processor in {retryDelaySeconds} seconds...");
                        await Task.Delay(retryDelaySeconds * 1000, cancellationTokenSource.Token);
                    }
                }
            }, cancellationTokenSource.Token);
        }

        private async Task StopInternalAsync(CancellationToken cancellationToken)
        {
            if (_cancellationTokenSource != null)
            {
                if (_runningTask != null)
                {
                    _logger.LogInformation($"{_typeName}.StopInternalAsync: Cancelling CTS.");
                    _cancellationTokenSource.Cancel();
                    try
                    {
                        _logger.LogInformation($"{_typeName}.StopInternalAsync: Awaiting the running task to allow it to gracefully stop...");
                        await _runningTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation($"{_typeName}.StopInternalAsync: Awaited task threw OperationCanceledException (this is normal).");
                    }
                    finally
                    {
                        _logger.LogInformation($"{_typeName}.StopInternalAsync: Clearing _runningTask to null.");
                        _runningTask = null;
                    }
                }
                _logger.LogInformation($"{_typeName}.StopInternalAsync: Disposing CTS.");
                _cancellationTokenSource.Dispose();
                _logger.LogInformation($"{_typeName}.StopInternalAsync: Clearing CTS to null.");
                _cancellationTokenSource = null;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"{_typeName}.StopAsync: Deferring to StopInternalAsync.");
            await StopInternalAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation($"{_typeName}.DisposeAsync: Deferring to StopAsync.");
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
