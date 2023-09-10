namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.Logging;
    using System.Runtime.CompilerServices;

    public class LoggingWorkerPoolTracer : WorkerPoolTracer
    {
        private readonly ILogger _logger;

        public LoggingWorkerPoolTracer(ILogger logger)
        {
            _logger = logger;
        }

        public override void AddTracingMessage(string message, [CallerMemberName] string memberName = "")
        {
            _logger.LogTrace($"{memberName}: {message}");
        }
    }
}
