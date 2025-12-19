namespace UET.Commands.Storage.Purge
{
    using Redpoint.CommandLine;
    using Redpoint.Uet.Workspace.Storage;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal sealed class StorageAutoPurgeCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithInstance<StorageAutoPurgeCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("autopurge", "Automatically purge storage consumed by UET if low on disk space.");
                })
            .Build();

        private sealed class StorageAutoPurgeCommandInstance : ICommandInstance
        {
            private readonly IStorageManagement _storageManagement;

            public StorageAutoPurgeCommandInstance(IStorageManagement storageManagement)
            {
                _storageManagement = storageManagement;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                await _storageManagement.AutoPurgeStorageAsync(
                    context.GetCancellationToken()).ConfigureAwait(false);

                return 0;
            }
        }
    }
}
