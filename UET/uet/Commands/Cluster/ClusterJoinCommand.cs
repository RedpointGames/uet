using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.ServiceControl;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ServiceProcess;
using UET.Services;

namespace UET.Commands.Cluster
{
    internal sealed class ClusterJoinCommand
    {
        internal sealed class Options
        {
        }

        public static Command CreateClusterJoinCommand()
        {
            var options = new Options();
            var command = new Command(
                "join",
                "Join this machine to an existing Kubernetes cluster as a Kubernetes node.");
            command.FullDescription =
                """
                Joins this machine to an existing Kubernetes cluster as a Kubernetes node.

                This command can be run on Windows 11 and Linux machines. The controller of the cluster will be automatically discovered on the local network via UDP broadcast.

                This command will support macOS machines in the future, but currently does not.
                
                CLUSTERS ARE CONFIGURED ONLY FOR TRUSTED BUILD AND AUTOMATION WORKLOADS ON TRUSTED NETWORKS. Neither the cluster nor any workload should be exposed to the Internet or an untrusted network.
                """;
            command.AddAllOptions(options);
            command.AddCommonHandler<ClusterJoinCommandInstance>(options, services =>
            {
                services.AddSingleton<IRkmClusterControl, DefaultRkmClusterControl>();
            });
            return command;
        }

        private sealed class ClusterJoinCommandInstance : ICommandInstance
        {
            private readonly IRkmClusterControl _clusterControl;

            public ClusterJoinCommandInstance(IRkmClusterControl clusterControl)
            {
                _clusterControl = clusterControl;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var exitCode = await _clusterControl.CreateOrJoin(false);
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
