namespace Redpoint.Uet.Core
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Logging.SingleLine;
    using Redpoint.Logging.File;
    using Redpoint.Uet.Core.Permissions;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using Redpoint.Uet.Core.BugReport;

    public static class CoreServiceExtensions
    {
        public static bool SuppressAllLogging { get; set; } = false;

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
             Justification = "AddConsoleFormatter and RegisterProviderOptions are only dangerous when the Options type cannot be statically analyzed, but that is not the case here. " +
             "The DynamicallyAccessedMembers annotations on them will make sure to preserve the right members from the different options objects.")]
        public static void AddUetCore(
            this IServiceCollection services,
            bool omitLogPrefix = false,
            LogLevel minimumLogLevel = LogLevel.Information,
            bool skipLoggingRegistration = false,
            bool permitRunbackLogging = false,
            BugReportCollector? bugReportCollector = null)
        {
            services.AddSingleton<IStringUtilities, DefaultStringUtilities>();
            services.AddSingleton<IWorldPermissionApplier, DefaultWorldPermissionApplier>();

            if (SuppressAllLogging)
            {
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                });
            }
            else if (!skipLoggingRegistration)
            {
                services.AddLogging(builder =>
                {
                    var enableRunbackLogging = permitRunbackLogging && Environment.GetEnvironmentVariable("UET_RUNBACKS") == "1";
                    builder.ClearProviders();
                    builder.SetMinimumLevel(enableRunbackLogging || bugReportCollector != null ? LogLevel.Trace : minimumLogLevel);
                    builder.AddSingleLineConsoleFormatter(options =>
                    {
                        options.OmitLogPrefix = omitLogPrefix;
                        options.TimestampFormat = minimumLogLevel == LogLevel.Trace ? "HH:mm:ss.fff " : null;
                    });
                    builder.AddSingleLineConsole(options =>
                    {
                        options.IncludeTracing = enableRunbackLogging || bugReportCollector != null ? (minimumLogLevel == LogLevel.Trace) : true;
                    });

                    if (enableRunbackLogging)
                    {
                        Directory.CreateDirectory(RunbackGlobalState.RunbackDirectoryPath);

                        // Automatically delete runbacks older than 30 days so they don't consume space forever.
                        foreach (var file in new DirectoryInfo(RunbackGlobalState.RunbackDirectoryPath).GetFiles())
                        {
                            if (file.LastWriteTimeUtc < DateTime.UtcNow - TimeSpan.FromDays(30))
                            {
                                try
                                {
                                    file.Delete();
                                }
                                catch
                                {
                                }
                            }
                        }

                        builder.AddFile(new FileStream(RunbackGlobalState.RunbackLogPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete));
                    }

                    if (bugReportCollector != null)
                    {
                        builder.AddProvider(bugReportCollector.LoggerProvider);
                    }
                });
            }
        }
    }
}