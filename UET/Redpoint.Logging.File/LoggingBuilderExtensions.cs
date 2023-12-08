namespace Redpoint.Logging.File
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provides methods for registering and using the file logger.
    /// </summary>
    public static class LoggingBuilderExtensions
    {
        /// <summary>
        /// Adds the file logger to the logging builder.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="logFile">The file stream that logs will be emitted to. This stream is automatically disposed when the application exits.</param>
        /// <returns>The logging builder.</returns>
        public static ILoggingBuilder AddFile(this ILoggingBuilder builder, FileStream logFile)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>(services => new FileLoggerProvider(logFile));
            return builder;
        }
    }
}