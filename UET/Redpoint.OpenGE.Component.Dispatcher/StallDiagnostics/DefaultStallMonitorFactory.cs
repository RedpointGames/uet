namespace Redpoint.OpenGE.Component.Dispatcher.StallDiagnostics
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.Tasks;
    using System.Diagnostics;
    using System.Threading.Tasks;

    internal partial class DefaultStallMonitorFactory : IStallMonitorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultStallMonitorFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IStallMonitor CreateStallMonitor(
            ITaskSchedulerScope taskSchedulerScope,
            GraphExecutionInstance instance)
        {
            return new DefaultStallMonitor(
                _serviceProvider.GetRequiredService<ILogger<DefaultStallMonitor>>(),
                _serviceProvider.GetRequiredService<IStallDiagnostics>(),
                taskSchedulerScope,
                instance);
        }

        internal partial class DefaultStallMonitor : IStallMonitor
        {
            private readonly ILogger<DefaultStallMonitor> _logger;
            private readonly IStallDiagnostics _stallDiagnostics;
            private readonly GraphExecutionInstance _instance;
            private readonly CancellationTokenSource _cancellationTokenSource;
            private DateTime _lastMadeProgress;

            [LoggerMessage(
                Level = LogLevel.Warning,
                Message = "STALL DETECTED! DIAGNOSTICS:\n{diagnostics}")]
            static partial void LogStallDiagnostics(ILogger logger, string diagnostics);

            public DefaultStallMonitor(
                ILogger<DefaultStallMonitor> logger,
                IStallDiagnostics stallDiagnostics,
                ITaskSchedulerScope taskSchedulerScope,
                GraphExecutionInstance instance)
            {
                _logger = logger;
                _stallDiagnostics = stallDiagnostics;
                _instance = instance;
                _lastMadeProgress = DateTime.UtcNow;
                _cancellationTokenSource = new CancellationTokenSource();
                _ = taskSchedulerScope.RunAsync(
                    "StallMonitor",
                    _cancellationTokenSource.Token,
                    async (cancellationToken) =>
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                            if ((DateTime.UtcNow - _lastMadeProgress).TotalSeconds > 120)
                            {
                                var diagnostics = await _stallDiagnostics.CaptureStallInformationAsync(
                                    instance).ConfigureAwait(false);
                                LogStallDiagnostics(_logger, diagnostics);
                                instance.CancelEntireBuildDueToException(new InvalidOperationException("STALL DETECTED!"));
                            }
                        }
                    });
            }

            public void MadeProgress()
            {
                _lastMadeProgress = DateTime.UtcNow;
            }

            public ValueTask DisposeAsync()
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
