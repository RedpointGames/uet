namespace UET.Commands.Internal.WindowsImaging
{
    using Redpoint.CommandLine;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;
    using UET.Commands.Cluster;
    using UET.Commands.Internal.RunRemote;

    internal class WindowsImagingCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithCommand(
                builder =>
                {
                    builder.AddCommand<WindowsImagingCreatePxeBootCommand>();

                    return new Command("windows-imaging", "Create various boot images for Windows clients.");
                })
            .Build();
    }
}
