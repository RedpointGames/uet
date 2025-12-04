namespace Redpoint.CloudFramework.Processor
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NodaTime;
    using Redpoint.CloudFramework.Locking;
    using Redpoint.CloudFramework.Repository;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ScheduledProcessorHostedService<T> : IHostedService, IAsyncDisposable where T : IScheduledProcessor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IGlobalRepository _globalRepository;
        private readonly IGlobalLockService _globalLockService;
        private readonly ILogger<ScheduledProcessorHostedService<T>> _logger;

        private CancellationTokenSource? _cancellationTokenSource = null;
        private Task? _runningTask = null;

        private static readonly string _typeName = $"ScheduledProcessorHostedService<{typeof(T).Name}>";

        public ScheduledProcessorHostedService(
            IServiceProvider serviceProvider,
            IGlobalRepository globalRepository,
            IGlobalLockService globalLockService,
            ILogger<ScheduledProcessorHostedService<T>> logger)
        {
            _serviceProvider = serviceProvider;
            _globalRepository = globalRepository;
            _globalLockService = globalLockService;
            _logger = logger;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var scheduledJobKey = await ScheduledJobModel.GetKey(_globalRepository, T.RoleName);
                var scheduledJob = await _globalRepository.LoadAsync<ScheduledJobModel>(string.Empty, scheduledJobKey, cancellationToken: cancellationToken);
                var lastCompletedDate = scheduledJob?.dateLastCompletedUtc ?? Instant.MinValue;
                var nextTime = T.CronExpression.GetNextOccurrence(lastCompletedDate.ToDateTimeUtc());

                var now = SystemClock.Instance.GetCurrentInstant();

                if (nextTime.HasValue && Instant.FromDateTimeUtc(nextTime.Value) < now)
                {
                    // Time has elapsed, we need to run.
                    try
                    {
                        _logger.LogInformation($"{_typeName}.RunAsync: Acquiring lock for scheduled job '{T.RoleName}'...");
                        await _globalLockService.AcquireAndUse(string.Empty, scheduledJobKey, async () =>
                        {
                            try
                            {
                                _logger.LogInformation($"{_typeName}.RunAsync: Executing scheduled job '{T.RoleName}'...");
                                var instance = _serviceProvider.GetRequiredService<T>();
                                await instance.ExecuteAsync(cancellationToken);
                                cancellationToken.ThrowIfCancellationRequested();

                                _logger.LogInformation($"{_typeName}.RunAsync: Scheduled processor completed successfully, updating date last completed...");
                                var scheduledJob = await _globalRepository.LoadAsync<ScheduledJobModel>(string.Empty, scheduledJobKey, cancellationToken: cancellationToken);
                                if (scheduledJob == null)
                                {
                                    scheduledJob = new ScheduledJobModel
                                    {
                                        Key = scheduledJobKey,
                                    };
                                }
                                scheduledJob.dateLastCompletedUtc = SystemClock.Instance.GetCurrentInstant();
                                await _globalRepository.UpsertAsync(string.Empty, scheduledJob);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"{_typeName}.RunAsync: Encountered an exception while running scheduled processor.");
                            }
                        });
                    }
                    catch (LockAcquisitionException)
                    {
                        _logger.LogInformation($"{_typeName}.RunAsync: Unable to acquire lock to run scheduled job '{T.RoleName}', another instance is executing this task. Waiting 5 minutes and then trying again...");
                        await Task.Delay(5 * 60 * 1000, cancellationToken);
                    }
                }
                else
                {
                    // Wait up to one hour.
                    var timeToWait = nextTime.HasValue
                        ? Duration.Min((Instant.FromDateTimeUtc(nextTime.Value) - now), Duration.FromHours(1))
                        : Duration.FromHours(1);
                    _logger.LogInformation($"{_typeName}.RunAsync: Not yet time to run scheduled job, waiting {timeToWait}.");
                    await Task.Delay((int)Math.Ceiling(timeToWait.TotalMilliseconds), cancellationToken);
                }
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await StopInternalAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation($"{_typeName}.StartAsync: Creating new CTS.");
            var cancellationTokenSource = _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _logger.LogInformation($"{_typeName}.StartAsync: Starting the running task via Task.Run.");
            _runningTask = Task.Run(() =>
            {
                _logger.LogInformation($"{_typeName}.StartAsync: Calling ExecuteAsync inside Task.Run.");
                return RunAsync(cancellationTokenSource.Token);
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
