namespace Redpoint.CloudFramework.CLI
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using System;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    internal class YarnInstallCaptureSpecification : ICaptureSpecification
    {
        private readonly ILogger _logger;
        private readonly YarnLogEntryJsonSerializerContext _serializer;

        private static readonly Dictionary<string, string> _replacements = new Dictionary<string, string>
        {
            { "┬╖", "-" },
            { "Γöî", "/" },
            { "Γöö", "\\" },
            { "Γöé", "|" },
        };
        private static readonly Regex _consoleRegex = new Regex("\x1b\\[[^;];");

        public YarnInstallCaptureSpecification(ILogger logger)
        {
            _logger = logger;
            _serializer = new YarnLogEntryJsonSerializerContext();
        }

        public bool InterceptStandardInput => false;

        public bool InterceptStandardOutput => true;

        public bool InterceptStandardError => false;

        public void OnReceiveStandardError(string data)
        {
            throw new NotImplementedException();
        }

        public void OnReceiveStandardOutput(string data)
        {
            YarnLogEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize(data, _serializer.YarnLogEntry);
            }
            catch
            {
                return;
            }
            if (entry == null)
            {
                return;
            }

            var indent = entry.Indent ?? string.Empty;
            var message = entry.Data ?? string.Empty;
            indent = _consoleRegex.Replace(indent, string.Empty);
            message = _consoleRegex.Replace(message, string.Empty);
            foreach (var replacement in _replacements)
            {
                indent = indent.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
                message = message.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
            }

            switch (entry.Type)
            {
                case "info":
                    _logger.LogInformation($"{entry.DisplayName} {indent}{message}");
                    break;
                case "warning":
                    _logger.LogWarning($"{entry.DisplayName} {indent}{message}");
                    break;
                case "error":
                default:
                    _logger.LogError($"{entry.DisplayName} {indent}{message}");
                    break;
            }
        }

        public string? OnRequestStandardInputAtStartup()
        {
            throw new NotImplementedException();
        }
    }
}
