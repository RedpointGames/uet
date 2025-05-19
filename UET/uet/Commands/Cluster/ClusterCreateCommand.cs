using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.ServiceControl;
using System.CommandLine;
using System.CommandLine.Invocation;
using UET.Services;

namespace UET.Commands.Cluster
{
    internal sealed class ClusterCreateCommand
    {
        internal sealed class Options
        {
        }

        public static Command CreateClusterCreateCommand()
        {
            var options = new Options();
            var command = new Command(
                "create",
                "Create a new Kubernetes cluster with this machine as the controller.");
            command.FullDescription =
                """
                Create a new Kubernetes cluster with this machine as the controller.

                This command can only be run on Linux machines as Kubernetes controller components can only run on Linux. It must be run before 'uet cluster join' can be used.

                CLUSTERS ARE CONFIGURED ONLY FOR TRUSTED BUILD AND AUTOMATION WORKLOADS ON TRUSTED NETWORKS. Neither the cluster nor any workload should be exposed to the Internet or an untrusted network.
                """;
            command.AddAllOptions(options);
            command.AddCommonHandler<ClusterCreateCommandInstance>(options, services =>
            {
                services.AddSingleton<IRkmClusterControl, DefaultRkmClusterControl>();
            });
            return command;
        }

        private sealed class ClusterCreateCommandInstance : ICommandInstance
        {
            private readonly IRkmClusterControl _clusterControl;

            public ClusterCreateCommandInstance(IRkmClusterControl clusterControl)
            {
                _clusterControl = clusterControl;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var exitCode = await _clusterControl.CreateOrJoin(true);
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
