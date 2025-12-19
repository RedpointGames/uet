using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.CommandLine;
using Redpoint.KubernetesManager.Services;
using Redpoint.ServiceControl;
using System.CommandLine;
using System.CommandLine.Invocation;
using UET.Services;

namespace UET.Commands.Cluster
{
    internal sealed class ClusterStopCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithInstance<ClusterStopCommandInstance>()
            .WithCommand(
                builder =>
                {
                    var command = new Command(
                        "stop",
                        "Stops Kubernetes services running on this machine.");
                    command.FullDescription =
                        """
                        Stops Kubernetes services running on this machine.

                        If this machine is the Kubernetes controller, the cluster will stop being operational. Otherwise, this Kubernetes node will no longer be ready in the cluster and can not have work scheduled on it. All existing workloads will terminate.

                        This command will also disable the underlying RKM service so that it does not run when the machine starts up. If you want to enable the service again, use 'uet cluster start'.
                        """;
                    return command;
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    services.AddSingleton<IRkmClusterControl, DefaultRkmClusterControl>();
                    services.AddSingleton<IRkmGlobalRootProvider, DefaultRkmGlobalRootProvider>();
                })
            .Build();

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

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                foreach (var service in new[] { "rkm", "rkm-containerd", "rkm-kubelet" })
                {
                    if (await _serviceControl.IsServiceInstalled(service))
                    {
                        if (await _serviceControl.IsServiceRunning(service))
                        {
                            _logger.LogInformation($"Stopping '{service}' service...");
                            await _serviceControl.StopService(service);
                        }
                        else
                        {
                            _logger.LogInformation($"The service '{service}' is already stopped.");
                        }

                        _logger.LogInformation($"Uninstalling '{service}' service...");
                        await _serviceControl.UninstallService(service);
                    }
                    else
                    {
                        _logger.LogInformation($"The service '{service}' is not currently installed.");
                    }
                }

                return 0;
            }
        }
    }

}
