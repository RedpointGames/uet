namespace Redpoint.Logging.Mac
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System.Collections.Concurrent;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("macos")]
    [ProviderAlias("Mac")]
    internal class MacLoggerProvider : ILoggerProvider
    {
        private readonly IOptionsMonitor<MacLoggerOptions> _options;
        private readonly ConcurrentDictionary<string, MacLogger> _loggers;

        public MacLoggerProvider(IOptionsMonitor<MacLoggerOptions> options)
        {
            _options = options;
            _loggers = new ConcurrentDictionary<string, MacLogger>();
        }

        public ILogger CreateLogger(string name)
        {
            return _loggers.TryGetValue(name, out MacLogger? logger) ? logger : _loggers.GetOrAdd(name, new MacLogger(name, _options.CurrentValue));
        }

        public void Dispose()
        {
        }
    }
}