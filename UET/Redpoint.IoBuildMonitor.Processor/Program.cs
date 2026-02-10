extern alias RDCommandLine;

namespace Io.Processor
{
    using Io.Database;
    using Io.Mappers;
    using Io.Redis;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.Logging.SingleLine;
    using StackExchange.Redis;
    using System.Diagnostics.CodeAnalysis;
    using RDCommandLine::Microsoft.Extensions.Logging.Console;

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Register our additional database related services.
                    services.AddDatabaseServices();

                    // Register mappers for converting from GitLab JSON to EF.
                    services.AddMappers();

                    // Register the Postgres database connection, which is used for log storage.
                    services.AddIoDbContext(hostContext.HostingEnvironment, hostContext.Configuration);

                    services.AddSingleton<ConnectionMultiplexer>(ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS_SERVER") ?? "localhost:6379"));
                    services.AddSingleton<INotificationHub, RedisNotificationHub>();

                    services.AddHostedService<ApplyDbMigrationsHostedService>();
                    services.AddProcessors();

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