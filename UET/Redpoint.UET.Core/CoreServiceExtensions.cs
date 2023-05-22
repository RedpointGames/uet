using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Redpoint.UET.Core
{
    public static class CoreServiceExtensions
    {
        public static void AddUETCore(this IServiceCollection services)
        {
            services.AddSingleton<IStringUtilities, DefaultStringUtilities>();

            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddConsoleFormatter<SimpleBuildConsoleFormatter, SimpleConsoleFormatterOptions>(options =>
                {
                    options.ColorBehavior = LoggerColorBehavior.Default;
                    options.SingleLine = true;
                    options.IncludeScopes = false;
                    options.TimestampFormat = "HH:mm:ss ";
                });
                builder.AddConsole(options =>
                {
                    options.FormatterName = "simple-build";
                });
            });
        }
    }
}