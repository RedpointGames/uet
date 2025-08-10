namespace Redpoint.Logging.SingleLine
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Logging.Console;
    using Microsoft.Extensions.Options;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using static Crayon.Output;

    /// <summary>
    /// Based on https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Console/src/SimpleConsoleFormatter.cs because
    /// we need to modify it in unsupported ways.
    /// </summary>
    internal sealed class SimpleBuildConsoleFormatter : ConsoleFormatter, IDisposable
    {
        private const string _loglevelPadding = ":";
        private static readonly string _messagePadding = new string(' ', GetLogLevelString(LogLevel.Information).Length + _loglevelPadding.Length);
        private static readonly string _newLineWithMessagePadding = Environment.NewLine + _messagePadding;
#if NETCOREAPP
        private static bool IsAndroidOrAppleMobile => OperatingSystem.IsAndroid() ||
                                                      OperatingSystem.IsTvOS() ||
                                                      OperatingSystem.IsIOS(); // returns true on MacCatalyst
#else
        private static bool IsAndroidOrAppleMobile => false;
#endif
        private readonly IDisposable? _optionsReloadToken;

        public SimpleBuildConsoleFormatter(IOptionsMonitor<ExtendedSimpleConsoleFormatterOptions> options)
            : base("redpoint-singleline")
        {
            ReloadLoggerOptions(options.CurrentValue);
            _optionsReloadToken = options.OnChange(ReloadLoggerOptions);
        }

        [MemberNotNull(nameof(FormatterOptions))]
        private void ReloadLoggerOptions(ExtendedSimpleConsoleFormatterOptions options)
        {
            FormatterOptions = options;
        }

        public void Dispose()
        {
            _optionsReloadToken?.Dispose();
        }

        internal ExtendedSimpleConsoleFormatterOptions FormatterOptions { get; set; }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            string message = logEntry.Formatter(logEntry.State, logEntry.Exception);
            if (logEntry.Exception == null && message == null)
            {
                return;
            }
            LogLevel logLevel = logEntry.LogLevel;
            Func<string, string> logLevelColors = GetLogLevelConsoleColors(logLevel);
            string logLevelString = GetLogLevelString(logLevel);

            string? timestamp = null;
            string? timestampFormat = FormatterOptions.TimestampFormat;
            if (timestampFormat != null)
            {
                DateTimeOffset dateTimeOffset = GetCurrentDateTime();
                timestamp = dateTimeOffset.ToString(timestampFormat, CultureInfo.InvariantCulture);
            }
            if (!FormatterOptions.OmitLogPrefix)
            {
                if (timestamp != null)
                {
                    textWriter.Write(timestamp);
                }
                if (logLevelString != null)
                {
                    textWriter.Write("[");
                    WriteColoredMessage(textWriter, logLevelString, logLevelColors);
                    textWriter.Write("]");
                }
            }
            CreateDefaultLogMessage(textWriter, logEntry, message, scopeProvider);
        }

        private static void WriteColoredMessage(TextWriter textWriter, string message, Func<string, string> colorise)
        {
            textWriter.Write(colorise(message));
        }

        private void CreateDefaultLogMessage<TState>(TextWriter textWriter, in LogEntry<TState> logEntry, string message, IExternalScopeProvider? scopeProvider)
        {
            bool singleLine = FormatterOptions.SingleLine;
            Exception? exception = logEntry.Exception;

            // Example:
            // info: ConsoleApp.Program[10]
            //       Request received

            // category and event id
            //textWriter.Write("");
            if (!singleLine)
            {
                textWriter.Write(Environment.NewLine);
            }

            // scope information
            WriteScopeInformation(textWriter, scopeProvider, singleLine);
            WriteMessage(textWriter, message, singleLine, FormatterOptions.OmitLogPrefix);

            // Example:
            // System.InvalidOperationException
            //    at Namespace.Class.Function() in File:line X
            if (exception != null)
            {
                // exception message
                WriteMessage(textWriter, exception.ToString(), singleLine, FormatterOptions.OmitLogPrefix);
            }
            if (singleLine)
            {
                textWriter.Write(Environment.NewLine);
            }
        }

        private static void WriteMessage(TextWriter textWriter, string message, bool singleLine, bool omitLogPrefix)
        {
            if (!string.IsNullOrEmpty(message))
            {
                if (singleLine)
                {
                    if (!omitLogPrefix)
                    {
                        textWriter.Write(' ');
                    }
                    WriteReplacing(textWriter, Environment.NewLine, " ", message);
                }
                else
                {
                    textWriter.Write(_messagePadding);
                    WriteReplacing(textWriter, Environment.NewLine, _newLineWithMessagePadding, message);
                    textWriter.Write(Environment.NewLine);
                }
            }

            static void WriteReplacing(TextWriter writer, string oldValue, string newValue, string message)
            {
                string newMessage = message.Replace(oldValue, newValue, StringComparison.Ordinal);
                writer.Write(newMessage);
            }
        }

        private DateTimeOffset GetCurrentDateTime()
        {
            return FormatterOptions.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
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

        private static Func<string, string> GetLogLevelConsoleColors(LogLevel logLevel)
        {
            if (OperatingSystem.IsWindows() &&
                Environment.GetEnvironmentVariable("CI") != "true" &&
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WT_SESSION")))
            {
                // Use legacy console colors on Windows where we might be running under an older conhost.
                const string suffix = "\u001b[39m\u001b[22m\u001b[49m\u001b[0m";
                return logLevel switch
                {
                    LogLevel.Trace => x => $"\u001b[37m{x}{suffix}",
                    LogLevel.Debug => x => $"\u001b[37m{x}{suffix}",
                    LogLevel.Information => x => $"\u001b[1m\u001b[32m{x}{suffix}",
                    LogLevel.Warning => x => $"\u001b[1m\u001b[33m{x}{suffix}",
                    LogLevel.Error => x => $"\u001b[41m\u001b[30m{x}{suffix}",
                    LogLevel.Critical => x => $"\u001b[41m\u001b[30m{x}{suffix}",
                    _ => x => x,
                };
            }
            else
            {
                return logLevel switch
                {
                    LogLevel.Trace => x => Dim(White(x)),
                    LogLevel.Debug => x => Dim(White(x)),
                    LogLevel.Information => Bright.Green,
                    LogLevel.Warning => Bright.Yellow,
                    LogLevel.Error => x => Background.Red(Black(x)),
                    LogLevel.Critical => x => Background.Red(White(x)),
                    _ => x => x,
                };
            }
        }

        private void WriteScopeInformation(TextWriter textWriter, IExternalScopeProvider? scopeProvider, bool singleLine)
        {
            if (FormatterOptions.IncludeScopes && scopeProvider != null)
            {
                bool paddingNeeded = !singleLine;
                scopeProvider.ForEachScope((scope, state) =>
                {
                    if (paddingNeeded)
                    {
                        paddingNeeded = false;
                        state.Write(_messagePadding);
                        state.Write("=> ");
                    }
                    else
                    {
                        state.Write(" => ");
                    }
                    state.Write(scope);
                }, textWriter);

                if (!paddingNeeded && !singleLine)
                {
                    textWriter.Write(Environment.NewLine);
                }
            }
        }
    }
}
