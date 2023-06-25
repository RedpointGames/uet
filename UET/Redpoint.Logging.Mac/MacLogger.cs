namespace Redpoint.Logging.Mac
{
    using Microsoft.Extensions.Logging;
    using System.Diagnostics;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("macos")]
    internal class MacLogger : ILogger
    {
        private string _name;
        private MacLoggerOptions _options;
        private readonly nint _logger;

        private const int _typeDefault = 0x00;

        public MacLogger(string name, MacLoggerOptions options)
        {
            _name = name;
            _options = options;
            _logger = MacNative.os_log_create(Process.GetCurrentProcess().ProcessName, _name);
        }

        public IDisposable? BeginScope<TState>(
            TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
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

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var prefix = GetLogLevelString(logLevel);
            MacNative.redpoint_os_log(_logger, _typeDefault, $"[{prefix}] {message}");
        }
    }
}