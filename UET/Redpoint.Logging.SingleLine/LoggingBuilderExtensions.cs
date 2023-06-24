namespace Redpoint.Logging.SingleLine
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Console;
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Provides methods for registering and using the single line console formatter.
    /// </summary>
    public static class LoggingBuilderExtensions
    {
        /// <summary>
        /// Adds the single line console formatter to the logging builder.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <returns>The logging builder.</returns>
        public static ILoggingBuilder AddSingleLineConsoleFormatter(this ILoggingBuilder builder)
        {
            builder.AddSingleLineConsoleFormatter(options =>
            {
                options.OmitLogPrefix = false;
            });
            return builder;
        }

        /// <summary>
        /// Adds the single line console formatter to the logging builder, with additional options.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="options">Additional options.</param>
        /// <returns>The logging builder.</returns>
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
             Justification = "AddConsoleFormatter and RegisterProviderOptions are only dangerous when the Options type cannot be statically analyzed, but that is not the case here. " +
             "The DynamicallyAccessedMembers annotations on them will make sure to preserve the right members from the different options objects.")]
        public static ILoggingBuilder AddSingleLineConsoleFormatter(this ILoggingBuilder builder, Action<ExtendedSimpleConsoleFormatterOptions> options)
        {
            builder.AddConsoleFormatter<SimpleBuildConsoleFormatter, ExtendedSimpleConsoleFormatterOptions>(o =>
            {
                o.ColorBehavior = LoggerColorBehavior.Default;
                o.SingleLine = true;
                o.IncludeScopes = false;
                o.TimestampFormat = "HH:mm:ss ";
                options(o);
            });
            return builder;
        }

        /// <summary>
        /// Calls <see cref="ConsoleLoggerExtensions.AddConsole(ILoggingBuilder)"/> and preconfigures it to use the single line console formatter.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <returns>The logging builder.</returns>
        public static ILoggingBuilder AddSingleLineConsole(this ILoggingBuilder builder)
        {
            builder.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.None;
            });
            return builder;
        }

        /// <summary>
        /// Calls <see cref="ConsoleLoggerExtensions.AddConsole(ILoggingBuilder)"/> and preconfigures it to use the single line console formatter, with additional options.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="options">Additional options.</param>
        /// <returns>The logging builder.</returns>
        public static ILoggingBuilder AddSingleLineConsole(this ILoggingBuilder builder,
            Action<ConsoleLoggerOptions> options)
        {
            builder.AddConsole(o =>
            {
                o.FormatterName = "redpoint-singleline";
                options(o);
            });
            return builder;
        }
    }
}
