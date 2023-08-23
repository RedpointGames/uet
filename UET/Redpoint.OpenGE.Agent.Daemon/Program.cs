namespace Redpoint.OpenGE.Agent.Daemon
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.Logging.Mac;
    using Redpoint.Logging.SingleLine;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Component.Dispatcher;
    using Redpoint.OpenGE.Component.Dispatcher.PreprocessorCacheAccessor;
    using Redpoint.OpenGE.Component.PreprocessorCache;
    using Redpoint.OpenGE.Component.Worker;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.AutoDiscovery;

    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // Create the builder.
            var builder = Host.CreateDefaultBuilder(args);

            // Do required initialization when we're running as a Windows service.
            if (OperatingSystem.IsWindows() && args.Contains("--service"))
            {
                builder.UseWindowsService(options =>
                {
                    options.ServiceName = "Incredibuild Agent";
                });
            }

            // Bind services.
            builder.ConfigureServices(services =>
            {
                services.AddAllServices(args);
            });

            // Now build and run the host.
            var host = builder.Build();
            await host.RunAsync();
            return 0;
        }

        public static void AddAllServices(this IServiceCollection services, string[] args)
        {
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(args.Contains("--trace") ? LogLevel.Trace : LogLevel.Information);
                logging.AddSingleLineConsoleFormatter();
                logging.AddSingleLineConsole();

                if (OperatingSystem.IsMacOS())
                {
                    logging.AddMac();
                }

                if (OperatingSystem.IsWindows() && args.Contains("--service"))
                {
                    logging.AddEventLog(settings =>
                    {
#pragma warning disable CA1416
                        settings.SourceName = "OpenGE";
#pragma warning restore CA1416
                    });
                }
            });

            services.AddAutoDiscovery();
            services.AddGrpcPipes();
            services.AddProcessExecution();
            services.AddPathResolution();
            services.AddReservation();
            services.AddOpenGECore();
            services.AddOpenGEComponentDispatcher();
            services.AddOpenGEComponentWorker();
            services.AddOpenGEComponentPreprocessorCache();
            services.AddOpenGEAgent();
            services.AddSingleton<OpenGEHostedService>();
            services.AddHostedService(sp => sp.GetRequiredService<OpenGEHostedService>());
            services.AddSingleton<IPreprocessorCacheAccessor>(sp => sp.GetRequiredService<OpenGEHostedService>());
        }
    }
}

