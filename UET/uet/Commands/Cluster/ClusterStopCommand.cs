using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.KubernetesManager.Services;
using Redpoint.ServiceControl;
using System.CommandLine;
using System.CommandLine.Invocation;
using UET.Services;

namespace UET.Commands.Cluster
{
    internal sealed class ClusterStopCommand
    {
        internal sealed class Options
        {
        }

        public static Command CreateClusterStopCommand()
        {
            var options = new ClusterOptions();
            var command = new Command(
                "stop",
                "Stops Kubernetes services running on this machine.");
            command.FullDescription =
                """
                Stops Kubernetes services running on this machine.

                If this machine is the Kubernetes controller, the cluster will stop being operational. Otherwise, this Kubernetes node will no longer be ready in the cluster and can not have work scheduled on it. All existing workloads will terminate.

                This command will also disable the underlying RKM service so that it does not run when the machine starts up. If you want to enable the service again, use 'uet cluster start'.
                """;
            command.AddAllOptions(options);
            command.AddCommonHandler<ClusterStopCommandInstance>(options, services =>
            {
                services.AddSingleton<IRkmClusterControl, DefaultRkmClusterControl>();
                services.AddSingleton<IRkmGlobalRootProvider, DefaultRkmGlobalRootProvider>();
            });
            return command;
        }

        private sealed class ClusterStopCommandInstance : ICommandInstance
        {
            private readonly IServiceControl _serviceControl;
            private readonly ILogger<ClusterStopCommandInstance> _logger;

            public ClusterStopCommandInstance(
                IServiceControl serviceControl,
                ILogger<ClusterStopCommandInstance> logger)
            {
                _serviceControl = serviceControl;
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (await _serviceControl.IsServiceInstalled("rkm"))
                {
                    if (await _serviceControl.IsServiceRunning("rkm"))
                    {
                        _logger.LogInformation("Stopping RKM service...");
                        await _serviceControl.StopService("rkm");
                    }
                    else
                    {
                        _logger.LogInformation("The service is already stopped.");
                    }

                    _logger.LogInformation("Uninstalling RKM service, so it doesn't run at startup...");
                    await _serviceControl.UninstallService("rkm");
                }
                else
                {
                    _logger.LogInformation("The service is not currently installed.");
                }

                return 0;
            }
        }
    }

}
