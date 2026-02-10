extern alias RDCommandLine;

namespace Io
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Logging.SingleLine;
    using RDCommandLine::Microsoft.Extensions.Logging.Console;
    using Microsoft.AspNetCore.Server.Kestrel.Core;

    public static class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseSentry(o =>
                    {
                        o.Dsn = "https://6e115de29ae547318ff8085630ffebda@sentry.redpoint.games/10";
                        o.TracesSampleRate = 0.0;
                    });

                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.Limits.MinRequestBodyDataRate = new MinDataRate(20.0, TimeSpan.FromMinutes(1));
                    });

                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging(builder =>
                    {
                        builder.ClearProviders();
                        builder.SetMinimumLevel(LogLevel.Information);
                        builder.AddSingleLineConsoleFormatter(options =>
                        {
                            options.OmitLogPrefix = false;
                            options.ColorBehavior = hostContext.HostingEnvironment.IsProduction() ? LoggerColorBehavior.Disabled : LoggerColorBehavior.Default;
                        });
                        builder.AddSingleLineConsole();
                    });
                });
    }
}
