namespace Redpoint.KubernetesManager.HostedService.Kubelet
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.HostedService;
    using Redpoint.KubernetesManager.Manifest;
    using Redpoint.KubernetesManager.PerpetualProcess;
    using System;
    using System.CommandLine;

    public sealed class RunKubeletCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<RunKubeletCommandInstance>()
            .WithCommand(
                builder =>
                {
                    var command = new Command("run-kubelet");
                    command.IsHidden = true;
                    return command;
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        services.AddWindowsService(options =>
                        {
                            options.ServiceName = "rkm-kubelet";
                        });
                    }
                    services.AddRkmManifest();
                    services.AddRkmPerpetualProcess();
                    services.AddRkmHostedServiceEnvironment("rkm-kubelet");
                    services.AddHostedService<KubeletHostedService>();
                })
            .Build();

        internal sealed class Options
        {
            public Option<string> ManifestPath = new Option<string>("--manifest-path", "The path to the cached manifest file to use across restarts. This file will be read on startup, and written to whenever we receive a new manifest from the RKM service.");
        }
    }
}
