namespace Redpoint.Uet.Core
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Logging.SingleLine;
    using Redpoint.Uet.Core.Permissions;
    using System.Diagnostics.CodeAnalysis;

    public static class CoreServiceExtensions
    {
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
             Justification = "AddConsoleFormatter and RegisterProviderOptions are only dangerous when the Options type cannot be statically analyzed, but that is not the case here. " +
             "The DynamicallyAccessedMembers annotations on them will make sure to preserve the right members from the different options objects.")]
        public static void AddUETCore(this IServiceCollection services, bool omitLogPrefix = false, LogLevel minimumLogLevel = LogLevel.Information, bool skipLoggingRegistration = false)
        {
            services.AddSingleton<IStringUtilities, DefaultStringUtilities>();
            services.AddSingleton<IWorldPermissionApplier, DefaultWorldPermissionApplier>();

            if (!skipLoggingRegistration)
            {
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.SetMinimumLevel(minimumLogLevel);
                    builder.AddSingleLineConsoleFormatter(options =>
                    {
                        options.OmitLogPrefix = omitLogPrefix;
                    });
                    builder.AddSingleLineConsole();
                });
            }
        }
    }
}