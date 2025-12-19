using Microsoft.Extensions.Logging;
using Redpoint.CommandLine;
using Redpoint.KubernetesManager.Services;
using Redpoint.ServiceControl;
using System.CommandLine.Invocation;
using System.Diagnostics;
using UET.Services;

namespace UET.Commands.Cluster
{
    internal class DefaultRkmClusterControl : IRkmClusterControl
    {
        private readonly IServiceControl _serviceControl;
        private readonly ISelfLocation _selfLocation;
        private readonly IRkmGlobalRootProvider _rkmGlobalRootProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DefaultRkmClusterControl> _logger;

        public DefaultRkmClusterControl(
            IServiceControl serviceControl,
            ISelfLocation selfLocation,
            IRkmGlobalRootProvider rkmGlobalRootProvider,
            ILoggerFactory loggerFactory,
            ILogger<DefaultRkmClusterControl> logger)
        {
            _serviceControl = serviceControl;
            _selfLocation = selfLocation;
            _rkmGlobalRootProvider = rkmGlobalRootProvider;
            _loggerFactory = loggerFactory;
            _logger = logger;
        }

        public async Task<int> CreateOrJoin(ICommandInvocationContext context, ClusterOptions options)
        {
            // Compute the base directory of all RKM installs.
            Directory.CreateDirectory(_rkmGlobalRootProvider.RkmGlobalRoot);

            // Set up arguments so that the background service knows whether to run as a controller or not.
            var controller = context.ParseResult.GetValueForOption(options.Controller);
            var node = context.ParseResult.GetValueForOption(options.Node);
            var autoUpgrade = context.ParseResult.GetValueForOption(options.AutoUpgrade);
            var noAutoUpgrade = context.ParseResult.GetValueForOption(options.NoAutoUpgrade);
            var waitForSysprep = context.ParseResult.GetValueForOption(options.WaitForSysprep);
            var args = new List<string>();
            if (controller)
            {
                args = ["--controller"];
            }
            if (!string.IsNullOrWhiteSpace(node))
            {
                args = ["--node", node];
            }
            if (waitForSysprep)
            {
                args.Add("--wait-for-sysprep");
            }
            await File.WriteAllLinesAsync(Path.Combine(_rkmGlobalRootProvider.RkmGlobalRoot, "service-args"), args);

            // Toggle auto-upgrade feature by creating or deleting the service-auto-upgrade file.
            if (autoUpgrade)
            {
                try
                {
                    File.WriteAllText(Path.Combine(_rkmGlobalRootProvider.RkmGlobalRoot, "service-auto-upgrade"), "on");
                }
                catch
                {
                }
            }
            if (noAutoUpgrade)
            {
                try
                {
                    File.Delete(Path.Combine(_rkmGlobalRootProvider.RkmGlobalRoot, "service-auto-upgrade"));
                }
                catch
                {
                }
            }

            // Make sure the service is installed, up-to-date and started.
            string executablePathAndArguments;
            if (OperatingSystem.IsWindows())
            {
                executablePathAndArguments = $"{_selfLocation.GetUetLocalLocation(true)} internal rkm-service";
            }
            else if (OperatingSystem.IsLinux())
            {
                executablePathAndArguments = $"\"{_selfLocation.GetUetLocalLocation(true)}\" internal rkm-service";
            }
            else
            {
                _logger.LogError("This platform is not yet supported.");
                return 1;
            }

            var isServiceInstalled = await _serviceControl.IsServiceInstalled("rkm");
            if (isServiceInstalled)
            {
                var currentExecutableAndArguments = await _serviceControl.GetServiceExecutableAndArguments("rkm");
                if (currentExecutableAndArguments != executablePathAndArguments || args.Contains("--reinstall"))
                {
                    if (!_serviceControl.HasPermissionToInstall)
                    {
                        _logger.LogWarning("Unable to update service path because you are not running as Administrator/root.");
                    }
                    else
                    {
                        await _serviceControl.StopService("rkm");
                        await _serviceControl.UninstallService("rkm");
                        isServiceInstalled = false;
                    }
                }
            }
            if (!isServiceInstalled)
            {
                if (!_serviceControl.HasPermissionToInstall)
                {
                    _logger.LogError("You must be an Administrator/root to install the RKM service.");
                    return 1;
                }
                else
                {
                    _logger.LogInformation("Installing service...");
                    await _serviceControl.InstallService(
                        "rkm",
                        "RKM",
                        executablePathAndArguments);
                }
            }
            var isServiceStarted = await _serviceControl.IsServiceRunning("rkm");
            if (!isServiceStarted)
            {
                if (!_serviceControl.HasPermissionToStart)
                {
                    _logger.LogError("You must be an Administrator/root to start the RKM service.");
                    return 1;
                }
                else
                {
                    _logger.LogInformation("Starting service...");
                    await _serviceControl.StartService("rkm");
                }
            }
            else
            {
                _logger.LogInformation("RKM is already running, waiting for further service logs to arrive...");
            }

            return 0;
        }

        public async Task StreamLogs(CancellationToken cancellationToken)
        {
            // Now just monitor the process.
            if (OperatingSystem.IsWindows())
            {
#pragma warning disable CA1416 // Validate platform compatibility
                // Connect to the Event Log, monitor for RKM log entries, and emit.
                var eventLog = new EventLog("Application");
                cancellationToken.Register(() =>
                {
                    _logger.LogInformation("RKM will continue running in the background as a service.");
                    eventLog.EnableRaisingEvents = false;
                });
                var loggerCache = new Dictionary<string, ILogger>();
                eventLog.EnableRaisingEvents = true;
                eventLog.EntryWritten += (sender, args) =>
                {
                    if (args.Entry.Source.Equals("rkm", StringComparison.OrdinalIgnoreCase))
                    {
                        var lines = args.Entry.Message.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
                        var category = lines[0].Substring("Category:".Length).Trim();
                        var message = string.Join("\n", lines.Skip(3)).Trim();
                        if (!loggerCache.TryGetValue(category, out var logger))
                        {
                            logger = _loggerFactory.CreateLogger(category);
                            loggerCache.Add(category, logger);
                        }

                        switch (args.Entry.EntryType)
                        {
                            case EventLogEntryType.Information:
                            case EventLogEntryType.SuccessAudit:
                                logger.LogInformation(message);
                                break;
                            case EventLogEntryType.Warning:
                                logger.LogWarning(message);
                                break;
                            case EventLogEntryType.Error:
                            case EventLogEntryType.FailureAudit:
                                logger.LogError(message);
                                break;
                        }
                    }
                };
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(500, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
#pragma warning restore CA1416 // Validate platform compatibility
            }
            else
            {
                // Just use journalctl to monitor the service logs.
                var journalProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "/usr/bin/journalctl",
                    ArgumentList =
                    {
                        "-fu",
                        "rkm.service"
                    },
                    CreateNoWindow = true,
                    UseShellExecute = false,
                })!;
                try
                {
                    await journalProcess.WaitForExitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
                _logger.LogInformation("RKM will continue running in the background as a service.");
            }
        }
    }

}
