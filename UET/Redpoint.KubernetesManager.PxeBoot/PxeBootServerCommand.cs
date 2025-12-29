namespace Redpoint.KubernetesManager.PxeBoot
{
    using GitHub.JPMikkers.Dhcp;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.CommandLine;
    using Redpoint.Kestrel;
    using Redpoint.KubernetesManager.HostedService;
    using Redpoint.KubernetesManager.PxeBoot.Disk;
    using Redpoint.KubernetesManager.PxeBoot.Server;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class PxeBootServerCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithInstance<PxeBootServerCommandInstance>()
            .WithOptions<PxeBootServerOptions>()
            .WithCommand(
                builder =>
                {
                    return new Command("server", "Runs the server that serves PXE boot requests and files for provisioning machines.");
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    services.AddRkmHostedServiceEnvironment("rkm-pxeboot");
                    services.AddHostedService<PxeBootHostedService>();
                    services.AddDhcpServer();
                    services.AddKestrelFactory();
                })
            .Build();
    }
}
