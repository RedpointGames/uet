namespace Redpoint.Logging
{
    using Microsoft.Extensions.Logging;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Forwards logs from one service collection to an existing <see cref="ILogger"/> instance.
    /// </summary>
    [SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "This implementation does not have any resources to cleanup.")]
    public class ForwardingLoggerProvider : ILoggerProvider
    {
        private readonly ILogger _logger;

        private sealed class ForwardingLogger : ILogger
        {
            private readonly ILogger _logger;

            public ForwardingLogger(ILogger logger)
            {
                _logger = logger;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return _logger.BeginScope(state);
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return _logger.IsEnabled(logLevel);
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Information)
                {
                    logLevel = LogLevel.Trace;
                }
                _logger.Log(logLevel, eventId, state, exception, formatter);
            }
        }

        public ForwardingLoggerProvider(ILogger logger)
        {
            _logger = logger;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ForwardingLogger(_logger);
        }

        [SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "This implementation has no behaviour in Dispose.")]
        [SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "This implementation has no behaviour in Dispose.")]
        public void Dispose()
        {
        }
    }
}