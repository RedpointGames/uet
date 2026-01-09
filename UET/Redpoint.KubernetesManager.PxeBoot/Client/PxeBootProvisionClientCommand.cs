namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.PxeBoot.Bootmgr;
    using Redpoint.KubernetesManager.PxeBoot.Disk;
    using System.CommandLine;

    internal class PxeBootProvisionClientCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithOptions<PxeBootProvisionClientOptions>()
            .WithInstance<PxeBootProvisionClientCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("provision-client", "Provision this client via PXE boot.");
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    services.AddSingleton<IEfiBootManagerParser, DefaultEfiBootManagerParser>();
                    services.AddSingleton<IEfiBootManager, DefaultEfiBootManager>();
                    services.AddPxeBootProvisioning();
                })
            .Build();
    }
}
