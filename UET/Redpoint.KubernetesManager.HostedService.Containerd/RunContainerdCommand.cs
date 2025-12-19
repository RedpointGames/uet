namespace UET.Commands.Cluster
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.HostedService;
    using Redpoint.KubernetesManager.Manifest;
    using System;
    using System.CommandLine;

    public sealed class RunContainerdCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<RunContainerdCommandInstance>()
            .WithCommand(
                builder =>
                {
                    var command = new Command("run-containerd");
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
                            options.ServiceName = "rkm-containerd";
                        });
                    }
                    services.AddRkmManifest();
                    services.AddRkmPerpetualProcess();
                    services.AddRkmHostedServiceEnvironment("rkm-containerd");
                    services.AddHostedService<ContainerdHostedService>();
                })
            .Build();

        internal sealed class Options
        {
            public Option<string> ManifestPath = new Option<string>("--manifest-path", "The path to the cached manifest file to use across restarts. This file will be read on startup, and written to whenever we receive a new manifest from the RKM service.");
        }
    }
}
