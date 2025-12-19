using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.CommandLine;
using Redpoint.KubernetesManager.Services;
using Redpoint.ServiceControl;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace UET.Commands.Cluster
{
    internal sealed class ClusterLogsCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithInstance<ClusterLogsCommandInstance>()
            .WithCommand(
                builder =>
                {
                    var command = new Command(
                        "logs",
                        "View the logs for this Kubernetes node; must be already joined.");
                    command.FullDescription =
                        """
                        View the logs of Kubernetes processes and components running on this machine.
                        
                        This command streams logs of the Kubernetes services running on this machine for diagnostics. The local machine must already be joined to the cluster either via 'uet cluster create' or 'uet cluster join'. This command can't be used on an unrelated machine that is not part of the cluster.
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

        private sealed class ClusterLogsCommandInstance : ICommandInstance
        {
            private readonly IRkmClusterControl _clusterControl;
            private readonly IServiceControl _serviceControl;
            private readonly ILogger<ClusterLogsCommandInstance> _logger;

            public ClusterLogsCommandInstance(
                IRkmClusterControl clusterControl,
                IServiceControl serviceControl,
                ILogger<ClusterLogsCommandInstance> logger)
            {
                _clusterControl = clusterControl;
                _serviceControl = serviceControl;
                _logger = logger;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                if (!await _serviceControl.IsServiceRunning(OperatingSystem.IsWindows() ? "RKM" : "rkm"))
                {
                    _logger.LogError("The RKM service is not currently running.");
                    return 1;
                }

                await _clusterControl.StreamLogs(context.GetCancellationToken());
                return 0;
            }
        }
    }
}
