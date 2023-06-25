namespace Redpoint.Logging.Mac
{
    using Microsoft.Extensions.Logging;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("macos")]
    internal class MacLogger : ILogger
    {
        private string _name;
        private MacLoggerOptions _options;
        private readonly nint _logger;

        [DllImport("System", EntryPoint = "os_log_create")]
        private static extern nint os_log_create(string subsystem, string category);

        [DllImport("System", EntryPoint = "_os_log_internal")]
        private static extern nint _os_log_internal(
            nint dso,
            nint osLog,
            int type,
            [MarshalAs(UnmanagedType.LPStr)] string message);

        private const int _typeDefault = 0x00;
        private const int _typeInfo = 0x01;
        private const int _typeDebug = 0x02;
        private const int _typeError = 0x10;
        private const int _typeFault = 0x11;

        public MacLogger(string name, MacLoggerOptions options)
        {
            _name = name;
            _options = options;
            _logger = os_log_create(Process.GetCurrentProcess().ProcessName, _name);
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

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            switch (logLevel)
            {
                case LogLevel.Debug:
                case LogLevel.Trace:
                    _os_log_internal(IntPtr.Zero, _logger, _typeDebug, formatter(state, exception));
                    break;
                case LogLevel.Information:
                    _os_log_internal(IntPtr.Zero, _logger, _typeInfo, formatter(state, exception));
                    break;
                case LogLevel.Warning:
                    // @note: There is no warning level.
                    _os_log_internal(IntPtr.Zero, _logger, _typeInfo, formatter(state, exception));
                    break;
                case LogLevel.Error:
                    _os_log_internal(IntPtr.Zero, _logger, _typeError, formatter(state, exception));
                    break;
                case LogLevel.Critical:
                    _os_log_internal(IntPtr.Zero, _logger, _typeFault, formatter(state, exception));
                    break;
            }
        }
    }
}