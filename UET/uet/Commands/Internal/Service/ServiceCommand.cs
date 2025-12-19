namespace UET.Commands.Internal.Service
{
    using Redpoint.CommandLine;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Text;
    using UET.Commands.Cluster;

    internal class ServiceCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithCommand(
                builder =>
                {
                    builder.AddCommand<ServiceStartCommand>();
                    builder.AddCommand<ServiceStopCommand>();

                    return new Command("service", "Manage services.");
                })
            .Build();
    }
}
