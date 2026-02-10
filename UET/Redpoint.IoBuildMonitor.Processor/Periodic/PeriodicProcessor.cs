namespace Io.Processor.Periodic
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public abstract class PeriodicProcessor : IHostedService, IAsyncDisposable
    {
        private readonly ILogger<PeriodicProcessor> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _processingTask;

        internal PeriodicProcessor(ILogger<PeriodicProcessor> logger)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync(CancellationToken.None);

            _cancellationTokenSource.Dispose();

            GC.SuppressFinalize(this);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_processingTask != null)
            {
                throw new InvalidOperationException($"Periodic processor {this.GetType().FullName} is already started.");
            }

            _processingTask = Task.Run(async () => await RunAsync(_cancellationTokenSource.Token), cancellationToken);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_processingTask != null)
            {
                _cancellationTokenSource.Cancel();
                _logger.LogInformation("Waiting for processing to finish...");
                await _processingTask;
                _logger.LogInformation("Processing has finished.");
                _processingTask = null;
            }
        }

        protected abstract Task ExecuteAsync(long iteration, CancellationToken cancellationToken);

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            long iter = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ExecuteAsync(iter, cancellationToken);
                    iter++;
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected exception because we're stopping processing.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Unhandled exception in {this.GetType().FullName}: {ex.Message}");

                    // Delay so that a fast exception doesn't cause a spin loop.
                    await Task.Delay(10000, cancellationToken);
                }
            }
        }
    }
}
