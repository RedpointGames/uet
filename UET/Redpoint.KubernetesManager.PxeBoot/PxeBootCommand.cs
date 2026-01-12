namespace Redpoint.KubernetesManager.PxeBoot
{
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.PxeBoot.ActiveDirectory;
    using Redpoint.KubernetesManager.PxeBoot.Client;
    using Redpoint.KubernetesManager.PxeBoot.NotifyForReboot;
    using Redpoint.KubernetesManager.PxeBoot.Server;
    using System.CommandLine;

    public class PxeBootCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithCommand(
                builder =>
                {
                    builder.AddCommand<PxeBootProvisionClientCommand>();
                    builder.AddCommand<PxeBootMonitorClientCommand>();
                    builder.AddCommand<PxeBootServerCommand>();
                    builder.AddCommand<PxeBootNotifyForRebootCommand>();
                    builder.AddCommand<PxeBootActiveDirectoryIssuerCommand>();

                    return new Command("pxeboot", "Internal commands for PXE Boot.");
                })
            .Build();
    }
}
