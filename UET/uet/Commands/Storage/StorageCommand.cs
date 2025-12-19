namespace UET.Commands.Storage
{
    using Redpoint.CommandLine;
    using System.Collections.Generic;
    using System.CommandLine;
    using UET.Commands.New.Plugin;
    using UET.Commands.Storage.List;
    using UET.Commands.Storage.Purge;

    internal sealed class StorageCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithCommand(
                builder =>
                {
                    builder.AddCommand<StorageListCommand>();
                    builder.AddCommand<StoragePurgeCommand>();
                    builder.AddCommand<StorageAutoPurgeCommand>();

                    return new Command("storage", "View or remove storage used by UET.");
                })
            .Build();
    }
}
