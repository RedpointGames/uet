using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Redpoint.Concurrency;
using Redpoint.KubernetesManager;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using UET.Commands.Internal.RkmService;
using UET.Services;

namespace UET.Commands.Internal.Rkm
{
    internal sealed class RkmServiceCommand
    {
        internal sealed class Options
        {
        }

        public static Command CreateRkmServiceCommand()
        {
            var options = new Options();
            var command = new Command("rkm-service");
            command.AddAllOptions(options);
            command.AddCommonHandler<RunRkmServiceCommandInstance>(
                options,
                services =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        services.AddWindowsService(options =>
                        {
                            options.ServiceName = "RKM";
                        });

                        // @todo: This causes trim warnings!
                        // LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(services);
                    }
                    services.AddKubernetesManager();
                    services.AddSingleton<IRkmVersionProvider, UetRkmVersionProvider>();
                    services.AddSingleton<IHostApplicationLifetime>(sp => sp.GetRequiredService<RkmHostApplicationLifetime>());
                    services.AddSingleton<RkmHostApplicationLifetime, RkmHostApplicationLifetime>();
                    services.AddSingleton<IHostEnvironment, RkmHostEnvironment>();
                });
            return command;
        }

        private sealed class RkmHostEnvironment : IHostEnvironment
        {
            public RkmHostEnvironment(
                ISelfLocation selfLocation)
            {
                EnvironmentName = "Production";
                ApplicationName = "RKM";
                ContentRootPath = Path.GetDirectoryName(selfLocation.GetUetLocalLocation())!;
                ContentRootFileProvider = null!;
            }

            public string EnvironmentName { get; set; }
            public string ApplicationName { get; set; }
            public string ContentRootPath { get; set; }
            public IFileProvider ContentRootFileProvider { get; set; }
        }

        private sealed class RkmHostApplicationLifetime : IHostApplicationLifetime, IDisposable
        {
            public readonly CancellationTokenSource CtsStarted;
            public readonly CancellationTokenSource CtsStopping;
            public readonly CancellationTokenSource CtsStopped;
            public readonly Gate StopRequestedGate;

            public RkmHostApplicationLifetime()
            {
                CtsStarted = new CancellationTokenSource();
                CtsStopping = new CancellationTokenSource();
                CtsStopped = new CancellationTokenSource();
                StopRequestedGate = new Gate();
            }

            public CancellationToken ApplicationStarted => CtsStarted.Token;

            public CancellationToken ApplicationStopping => CtsStopping.Token;

            public CancellationToken ApplicationStopped => CtsStopped.Token;

            public void StopApplication()
            {
                StopRequestedGate.Open();
            }

            public void Dispose()
            {
                ((IDisposable)CtsStarted).Dispose();
                ((IDisposable)CtsStopping).Dispose();
                ((IDisposable)CtsStopped).Dispose();
            }
        }

        private sealed class RunRkmServiceCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunRkmServiceCommandInstance> _logger;
            private readonly Options _options;
            private readonly RkmHostApplicationLifetime _hostApplicationLifetime;
            private readonly IReadOnlyList<IHostedService> _hostedServices;
            private readonly IHostLifetime? _hostLifetime;

            public RunRkmServiceCommandInstance(
                ILogger<RunRkmServiceCommandInstance> logger,
                Options options,
                IEnumerable<IHostedService> hostedServices,
                RkmHostApplicationLifetime hostApplicationLifetime,
                IHostLifetime? hostLifetime = null)
            {
                _logger = logger;
                _options = options;
                _hostApplicationLifetime = hostApplicationLifetime;
                _hostedServices = hostedServices.ToList();
                _hostLifetime = hostLifetime;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                _logger.LogInformation("RKM is starting...");

                if (OperatingSystem.IsWindows() && WindowsServiceHelpers.IsWindowsService())
                {
                    _logger.LogInformation("Waiting for service start...");
                    await _hostLifetime!.WaitForStartAsync(context.GetCancellationToken());
                }

                try
                {
                    foreach (var hostedService in _hostedServices)
                    {
                        _logger.LogInformation($"Starting hosted service '{hostedService.GetType().FullName}'...");
                        await hostedService.StartAsync(context.GetCancellationToken());
                        _logger.LogInformation($"Started hosted service '{hostedService.GetType().FullName}'.");
                    }

                    if (OperatingSystem.IsWindows() && WindowsServiceHelpers.IsWindowsService())
                    {
                        // Tells the service lifetime that we have now started. The service
                        // lifetime will call StopApplication when we should shutdown, which
                        // will open the gate.
                        _hostApplicationLifetime.CtsStarted.Cancel();
                    }
                    else
                    {
                        // Wire up Ctrl-C to request stop.
                        if (context.GetCancellationToken().IsCancellationRequested)
                        {
                            _hostApplicationLifetime.StopRequestedGate.Open();
                        }
                        else
                        {
                            context.GetCancellationToken().Register(_hostApplicationLifetime.StopRequestedGate.Open);
                        }
                    }

                    // Wait until shutdown is requested.
                    await _hostApplicationLifetime.StopRequestedGate.WaitAsync(CancellationToken.None);
                }
                finally
                {
                    _logger.LogInformation("Shutting down service...");

                    if (OperatingSystem.IsWindows() && WindowsServiceHelpers.IsWindowsService())
                    {
                        _hostApplicationLifetime.CtsStopping.Cancel();
                    }

                    foreach (var hostedService in _hostedServices)
                    {
                        _logger.LogInformation($"Stopping hosted service '{hostedService.GetType().FullName}'...");
                        await hostedService.StopAsync(context.GetCancellationToken());
                        _logger.LogInformation($"Stopped hosted service '{hostedService.GetType().FullName}'.");
                    }

                    if (OperatingSystem.IsWindows() && WindowsServiceHelpers.IsWindowsService())
                    {
                        using var cts = new CancellationTokenSource(30000);
                        try
                        {
                            await _hostLifetime!.StopAsync(cts.Token);
                        }
                        catch
                        {
                        }
                        _hostApplicationLifetime.CtsStopped.Cancel();
                        _logger.LogInformation("Service has been stopped.");
                    }
                }

                return 0;
            }
        }
    }
}
