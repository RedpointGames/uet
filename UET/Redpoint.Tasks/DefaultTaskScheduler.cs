namespace Redpoint.Tasks
{
    using Microsoft.Extensions.Logging;

    internal sealed class DefaultTaskScheduler : ITaskScheduler
    {
        private readonly ILogger<DefaultTaskScheduler> _logger;

        public DefaultTaskScheduler(
            ILogger<DefaultTaskScheduler> logger)
        {
            _logger = logger;
        }

        public ITaskSchedulerScope CreateSchedulerScope(
            string scopeName,
            CancellationToken cancellationToken)
        {
            return new DefaultTaskSchedulerScope(
                _logger,
                scopeName,
                cancellationToken);
        }
    }
}