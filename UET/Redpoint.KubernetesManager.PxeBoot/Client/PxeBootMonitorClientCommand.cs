namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.HostedService;
    using Redpoint.KubernetesManager.PxeBoot.Api;
    using Redpoint.KubernetesManager.PxeBoot.Bootmgr;
    using Redpoint.KubernetesManager.PxeBoot.Disk;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Reboot;
    using Redpoint.Tpm;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Net.Http.Json;
    using System.Text;

    internal class PxeBootMonitorClientCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithOptions<PxeBootMonitorClientOptions>()
            .WithInstance<PxeBootMonitorClientCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("monitor-client", "Monitor when this client needs to be reprovisioned and reboots the current machine if so.");
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        services.AddWindowsService(options =>
                        {
                            options.ServiceName = "rkm-monitor";
                        });
                    }
                    services.AddRkmHostedServiceEnvironment("rkm-monitor");
                    services.AddHostedService<PxeBootMonitorClientHostedService>();
                    services.AddSingleton<IReboot, DefaultReboot>();
                    services.AddSingleton<IDurableOperation, DefaultDurableOperation>();
                    services.AddTpm();
                })
            .Build();
    }
}
