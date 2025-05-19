using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.KubernetesManager.Services;
using Redpoint.ServiceControl;
using System.CommandLine;
using System.CommandLine.Invocation;
using UET.Services;

namespace UET.Commands.Cluster
{
    internal sealed class ClusterStartCommand
    {
        public static Command CreateClusterStartCommand()
        {
            var options = new ClusterOptions();
            var command = new Command(
                "start",
                "Create or join a Kubernetes cluster with this machine as a Kubernetes node.");
            command.FullDescription =
                """
                Create or join a Kubernetes cluster with this machine as a Kubernetes node.

                If you specify --controller, this machine will run as the controller for a new Kubernetes cluster. [Linux only]
                If you specify --node <address>, this machine will join the existing cluster where the controller is running at the specified IP address.

                If you don't specify --controller or --node, UDP broadcast will be used to automatically discover an existing cluster. If none is found, this machine will run as a controller for a new cluster. If you're not passing either argument, then you must have run 'uet cluster start' on a Linux machine (or this must be a Linux machine) to create the initial controller node.

                CLUSTERS ARE CONFIGURED ONLY FOR TRUSTED BUILD AND AUTOMATION WORKLOADS ON TRUSTED NETWORKS. Neither the cluster nor any workload should be exposed to the Internet or an untrusted network.
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
            private readonly IRkmClusterControl _clusterControl;
            private readonly ClusterOptions _options;

            public ClusterStopCommandInstance(
                IRkmClusterControl clusterControl,
                ClusterOptions options)
            {
                _clusterControl = clusterControl;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var exitCode = await _clusterControl.CreateOrJoin(context, _options);
                if (exitCode != 0)
                {
                    return exitCode;
                }
                await _clusterControl.StreamLogs(context.GetCancellationToken());
                return 0;
            }
        }
    }

}
