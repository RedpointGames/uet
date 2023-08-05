namespace Redpoint.OpenGE.Component.Worker
{
    using Microsoft.Extensions.Logging;
    using System;

    internal class ForwardingLoggerProvider : ILoggerProvider
    {
        private readonly ILogger _logger;

        private class ForwardingLogger : ILogger
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

        public void Dispose()
        {
        }
    }
}
