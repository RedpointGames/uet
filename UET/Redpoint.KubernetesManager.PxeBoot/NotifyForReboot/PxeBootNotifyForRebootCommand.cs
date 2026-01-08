namespace Redpoint.KubernetesManager.PxeBoot.NotifyForReboot
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.PxeBoot.Client;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;

    internal class PxeBootNotifyForRebootCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithOptions<PxeBootNotifyForRebootOptions>()
            .WithInstance<PxeBootNotifyForRebootCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("notify-for-reboot", "Call when a reboot step should no longer boot into the custom iPXE script.");
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    services.AddSingleton<IProvisionContextDiscoverer, DefaultProvisionContextDiscoverer>();
                    services.AddSingleton<IDurableOperation, DefaultDurableOperation>();
                })
            .Build();
    }
}
