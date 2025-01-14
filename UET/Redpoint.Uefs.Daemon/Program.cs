﻿namespace Redpoint.Uefs.Daemon
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Vfs.Driver.WinFsp;
    using Redpoint.Vfs.Layer.Folder;
    using Redpoint.Vfs.Layer.Scratch;
    using Sentry;
    using Redpoint.Uefs.Daemon.PackageFs;
    using Redpoint.Uefs.Daemon.PackageStorage;
    using Redpoint.Uefs.Daemon.RemoteStorage;
    using Redpoint.Uefs.Daemon.Transactional;
    using Redpoint.Uefs.Package;
    using Redpoint.Uefs.Daemon.Service;
    using Redpoint.Uefs.Daemon.Integration.Docker;
    using Redpoint.Uefs.Daemon.Transactional.Executors;
    using Redpoint.Logging.SingleLine;
    using Redpoint.Uefs.Package.Vhd;
    using Redpoint.Uefs.Package.SparseImage;
    using Redpoint.Logging.Mac;
    using Redpoint.Vfs.LocalIo;
    using Redpoint.Tasks;
    using Redpoint.GrpcPipes.Transport.Tcp;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;

    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            using (SentrySdk.Init(o =>
            {
                o.Dsn = "https://d5031f96b67e4274ac845c20453eb4e1@errors.redpoint.games/8";
                o.IsGlobalModeEnabled = false;
                o.SendDefaultPii = false;
                o.TracesSampleRate = 1.0;
            }))
            {
                // Create the builder.
                var builder = Host.CreateDefaultBuilder(args);

                // Do required initialization when we're running as a Windows service.
                if (OperatingSystem.IsWindows() && args.Contains("--service"))
                {
                    builder.UseWindowsService(options =>
                    {
                        options.ServiceName = "UEFS Service";
                    });
                }

                // Bind services.
                builder.ConfigureServices(services =>
                {
                    services.AddAllServices(args);
                });

                // Now build and run the host.
                var host = builder.Build();
                await host.RunAsync().ConfigureAwait(false);
                return 0;
            }
        }

        public static void AddAllServices(this IServiceCollection services, string[] args)
        {
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(OperatingSystem.IsMacOS() ? LogLevel.Trace : LogLevel.Information);
                logging.AddSingleLineConsoleFormatter();
                logging.AddSingleLineConsole();

                if (OperatingSystem.IsMacOS())
                {
                    logging.AddMac();
                }

                logging.AddSentry(o =>
                {
                    o.Dsn = "https://d5031f96b67e4274ac845c20453eb4e1@errors.redpoint.games/8";
                    o.IsGlobalModeEnabled = false;
                    o.SendDefaultPii = false;
                    o.TracesSampleRate = 1.0;
                });

                if (OperatingSystem.IsWindows() && args.Contains("--service"))
                {
                    logging.AddEventLog(settings =>
                    {
#pragma warning disable CA1416
                        settings.SourceName = "UEFS";
#pragma warning restore CA1416
                    });
                }
            });
            services.AddGrpcPipes<TcpGrpcPipeFactory>();
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                services.AddWinFspVfsDriver();
            }
            services.AddPathResolution();
            services.AddProcessExecution();
            services.AddTasks();
            services.AddFolderLayerFactory();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();
            services.AddUefsPackage();
            services.AddUefsPackageVhd();
            services.AddUefsPackageSparseImage();
            services.AddUefsPackageFs();
            services.AddUefsPackageStorage();
            services.AddUefsRemoteStorage();
            services.AddUefsService();
            services.AddUefsDaemonTransactional();
            services.AddUefsDaemonTransactionalExecutors();
            services.AddUefsDaemonIntegrationDocker();
            services.AddSingleton<UefsHostedService>();
            services.AddHostedService(sp => sp.GetRequiredService<UefsHostedService>());
            services.AddHostedService<UefsHealthCheckService>();
            services.AddTransient(sp => sp.GetRequiredService<UefsHostedService>().UefsDaemon);
            services.AddTransient<IMountTracking>(sp => sp.GetRequiredService<IUefsDaemon>());
        }
    }
}

