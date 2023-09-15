namespace Redpoint.Logging.File
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Globalization;
    using System.Text;

    internal class FileLoggerProvider : ILoggerProvider, IDisposable
    {
        private readonly StreamWriter _streamWriter;
        private readonly Concurrency.Mutex _mutex;

        public FileLoggerProvider(FileStream stream)
        {
            _streamWriter = new StreamWriter(stream, Encoding.UTF8);
            _mutex = new Concurrency.Mutex();
        }

        private class FileLogger : ILogger
        {
            private FileLoggerProvider _provider;
            private string _categoryName;

            public FileLogger(FileLoggerProvider fileLoggerProvider, string categoryName)
            {
                _provider = fileLoggerProvider;
                _categoryName = categoryName;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            private static string GetLogLevelString(LogLevel logLevel)
            {
                return logLevel switch
                {
                    LogLevel.Trace => "trce",
                    LogLevel.Debug => "dbug",
                    LogLevel.Information => "info",
                    LogLevel.Warning => "warn",
                    LogLevel.Error => "fail",
                    LogLevel.Critical => "crit",
                    _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
                };
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                using (_provider._mutex.Wait(CancellationToken.None))
                {
                    var logTime = DateTime.Now;
                    _provider._streamWriter.WriteLine($"[{logTime.ToString("yyyy-MM-dd HH:mm:ss.FFF", CultureInfo.InvariantCulture)}] [{GetLogLevelString(logLevel)}] [{_categoryName}] {formatter(state, exception)}");
                }
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(this, categoryName);
        }

        public void Dispose()
        {
            using (_mutex.Wait(CancellationToken.None))
            {
                _streamWriter.Flush();
                _streamWriter.Dispose();
            }
        }
    }
}