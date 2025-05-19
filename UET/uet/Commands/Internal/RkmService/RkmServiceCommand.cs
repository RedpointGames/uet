using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Redpoint.Concurrency;
using Redpoint.KubernetesManager;
using Redpoint.KubernetesManager.Services;
using Redpoint.ProgressMonitor;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using UET.Commands.Internal.RkmService;
using UET.Commands.Upgrade;
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
                ContentRootPath = Path.GetDirectoryName(selfLocation.GetUetLocalLocation(true))!;
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
            private readonly IProgressFactory _progressFactory;
            private readonly IMonitorFactory _monitorFactory;
            private readonly IRkmGlobalRootProvider _rkmGlobalRootProvider;
            private readonly IReadOnlyList<IHostedService> _hostedServices;
            private readonly IHostLifetime? _hostLifetime;

            public RunRkmServiceCommandInstance(
                ILogger<RunRkmServiceCommandInstance> logger,
                Options options,
                IEnumerable<IHostedService> hostedServices,
                RkmHostApplicationLifetime hostApplicationLifetime,
                IProgressFactory progressFactory,
                IMonitorFactory monitorFactory,
                IRkmGlobalRootProvider rkmGlobalRootProvider,
                IHostLifetime? hostLifetime = null)
            {
                _logger = logger;
                _options = options;
                _hostApplicationLifetime = hostApplicationLifetime;
                _progressFactory = progressFactory;
                _monitorFactory = monitorFactory;
                _rkmGlobalRootProvider = rkmGlobalRootProvider;
                _hostedServices = hostedServices.ToList();
                _hostLifetime = hostLifetime;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (File.Exists(Path.Combine(_rkmGlobalRootProvider.RkmGlobalRoot, "service-auto-upgrade")))
                {
                    try
                    {
                        var lastCheck = DateTimeOffset.MinValue;
                        var lastCheckFile = Path.Combine(_rkmGlobalRootProvider.RkmGlobalRoot, "service-auto-upgrade-last-check");
                        try
                        {
                            lastCheck = DateTimeOffset.FromUnixTimeSeconds(long.Parse(File.ReadAllText(lastCheckFile).Trim(), CultureInfo.InvariantCulture));
                        }
                        catch
                        {
                        }

                        // Prevent us from running checks against GitHub too rapidly.
                        if (DateTimeOffset.UtcNow > lastCheck.AddMinutes(10))
                        {
                            _logger.LogInformation("RKM is checking for UET updates, and upgrading UET if necessary...");
                            var upgradeResult = await UpgradeCommandImplementation.PerformUpgradeAsync(
                                _progressFactory,
                                _monitorFactory,
                                _logger,
                                string.Empty,
                                true,
                                context.GetCancellationToken()).ConfigureAwait(false);
                            if (upgradeResult.CurrentVersionWasChanged)
                            {
                                _logger.LogInformation("UET has been upgraded and the version currently executing is no longer the latest version. RKM will now exit and expects the service manager (such as systemd) to automatically start it RKM as the new version.");
                                return 0;
                            }

                            // Only update the last check file if we didn't upgrade; this helps us get to the latest version faster if multiple upgrades are required.
                            File.WriteAllText(lastCheckFile, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            _logger.LogInformation("RKM already checked for UET upgrades in the last 10 minutes. Skipping automatic upgrade check.");
                        }
                    }
                    catch
                    {
                    }
                }
                else
                {
                    _logger.LogInformation("RKM is not automatically checking for updates. Run 'uet cluster start --auto-upgrade' to enable automatic updates.");
                }

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
