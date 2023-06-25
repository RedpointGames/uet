namespace Redpoint.Logging.Mac
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Configuration;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.Versioning;

    /// <summary>
    /// Extension methods for adding macOS logging.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static class MacLoggingExtensions
    {
        /// <summary>
        /// Adds a macOS logger named 'Mac' to the factory.
        /// </summary>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
        public static ILoggingBuilder AddMac(
            this ILoggingBuilder builder)
        {
            builder.AddConfiguration();

            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, MacLoggerProvider>());
            LoggerProviderOptions.RegisterProviderOptions<MacLoggerOptions, MacLoggerProvider>(builder.Services);

            return builder;
        }

        /// <summary>
        /// Adds a macOS logger named 'Mac' to the factory.
        /// </summary>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
        /// <param name="configure">A delegate to configure the <see cref="MacLogger"/>.</param>
        public static ILoggingBuilder AddMac(
            this ILoggingBuilder builder,
            Action<MacLoggerOptions> configure)
        {
            builder.AddMac();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}