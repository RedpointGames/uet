namespace UET.Commands.Cluster
{
    using k8s;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class ClusterTerminateStuckKubeProxyCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<ClusterTerminateStuckKubeProxyCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command(
                        "terminate-stuck-kube-proxy",
                        "Monitor pods and terminate kube-proxy Windows pods that are stuck.")
                    {
                        IsHidden = true,
                    };
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                })
            .Build();


        internal sealed class Options
        {
            public Option<bool> InCluster = new Option<bool>("--in-cluster");
        }

        private sealed class ClusterTerminateStuckKubeProxyCommandInstance : ICommandInstance
        {
            private readonly ILogger<ClusterTerminateStuckKubeProxyCommandInstance> _logger;
            private readonly Options _options;

            public ClusterTerminateStuckKubeProxyCommandInstance(
                ILogger<ClusterTerminateStuckKubeProxyCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                using var kubernetes = new Kubernetes(
                    context.ParseResult.GetValueForOption(_options.InCluster)
                        ? KubernetesClientConfiguration.InClusterConfig()
                        : KubernetesClientConfiguration.BuildDefaultConfig());

                try
                {
                    while (!context.GetCancellationToken().IsCancellationRequested)
                    {
                        _logger.LogInformation("Checking for stuck kube-proxy Windows pods...");

                        var pods = await kubernetes.ListNamespacedPodAsync(
                            "kube-system",
                            labelSelector: "rkm.redpoint.games/component=kube-proxy-windows",
                            cancellationToken: context.GetCancellationToken());

                        foreach (var pod in pods)
                        {
                            if (pod == null)
                            {
                                continue;
                            }

                            var allocateSourceVipContainer = (pod.Status?.InitContainerStatuses ?? [])
                                .FirstOrDefault(x => x.Name == "allocate-source-vip");
                            var mainContainer = (pod.Status?.ContainerStatuses ?? [])
                                .FirstOrDefault(x => x.Name == "kube-proxy");
                            if (allocateSourceVipContainer == null ||
                                mainContainer == null)
                            {
                                continue;
                            }

                            var mainContainerIsBroken = false;
                            if (mainContainer.State?.Waiting?.Reason == "PodInitializing" &&
                                mainContainer.LastState?.Terminated?.Reason == "ContainerStatusUnknown")
                            {
                                mainContainerIsBroken = true;
                            }
                            if (mainContainer.State?.Terminated?.Reason == "Unknown" &&
                                mainContainer.State?.Terminated?.ExitCode == 255)
                            {
                                mainContainerIsBroken = true;
                            }

                            if (allocateSourceVipContainer.Ready &&
                                mainContainerIsBroken)
                            {
                                _logger.LogInformation($"Detected stuck kube-proxy Windows pod '{pod.Metadata.Name}'. Deleting it...");

                                await kubernetes.DeleteNamespacedPodAsync(
                                    pod.Metadata.Name,
                                    "kube-system",
                                    cancellationToken: context.GetCancellationToken());
                            }
                        }

                        await Task.Delay(30000, context.GetCancellationToken());
                    }
                }
                catch (OperationCanceledException) when (context.GetCancellationToken().IsCancellationRequested)
                {
                }

                return 0;
            }
        }
    }
}
