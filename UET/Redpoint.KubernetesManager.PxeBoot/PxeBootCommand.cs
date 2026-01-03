namespace Redpoint.KubernetesManager.PxeBoot
{
    using Redpoint.CommandLine;
    using System.CommandLine;

    public class PxeBootCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithCommand(
                builder =>
                {
                    builder.AddCommand<PxeBootProvisionClientCommand>();
                    builder.AddCommand<PxeBootServerCommand>();

                    return new Command("pxeboot", "Internal commands for PXE Boot.");
                })
            .Build();
    }
}
