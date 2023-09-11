namespace UET.Commands.Uefs
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using Redpoint.ServiceControl;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Reflection;
    using System.Threading.Tasks;

    internal class UefsUninstallCommand
    {
        internal class Options
        {
        }

        public static Command CreateUninstallCommand()
        {
            var options = new Options();
            var command = new Command("uninstall", "Uninstall the UEFS daemon on this machine.");
            command.AddAllOptions(options);
            command.AddCommonHandler<UefsUninstallCommandInstance>(
                options,
                services =>
                {
                });
            return command;
        }

        private class UefsUninstallCommandInstance : ICommandInstance
        {
            private readonly ILogger<UefsUninstallCommandInstance> _logger;
            private readonly IServiceControl _serviceControl;
            private readonly IProgressFactory _progressFactory;
            private readonly IMonitorFactory _monitorFactory;

            public UefsUninstallCommandInstance(
                ILogger<UefsUninstallCommandInstance> logger,
                IServiceControl serviceControl,
                IProgressFactory progressFactory,
                IMonitorFactory monitorFactory)
            {
                _logger = logger;
                _serviceControl = serviceControl;
                _progressFactory = progressFactory;
                _monitorFactory = monitorFactory;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!_serviceControl.HasPermissionToInstall)
                {
                    _logger.LogError("This command must be run as an Administrator / root.");
                    return 1;
                }

                string daemonName;
                if (OperatingSystem.IsWindows())
                {
                    daemonName = "UEFS Service";
                }
                else if (OperatingSystem.IsMacOS())
                {
                    daemonName = "games.redpoint.UEFS";
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                if (await _serviceControl.IsServiceInstalled(daemonName).ConfigureAwait(false))
                {
                    if (await _serviceControl.IsServiceRunning(daemonName).ConfigureAwait(false))
                    {
                        _logger.LogInformation("Stopping UEFS daemon...");
                        await _serviceControl.StopService(daemonName).ConfigureAwait(false);
                    }

                    _logger.LogInformation("Uninstalling UEFS daemon...");
                    await _serviceControl.UninstallService(daemonName).ConfigureAwait(false);
                }

                _logger.LogInformation("The UEFS service has been uninstalled.");
                return 0;
            }
        }
    }
}
