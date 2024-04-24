namespace UET.Commands.Storage.Purge
{
    using Redpoint.CommandLine;
    using Redpoint.Uet.Workspace.Storage;
    using System.CommandLine;
    using System.Threading.Tasks;

    internal static class StorageAutoPurgeCommand
    {
        internal sealed class Options
        {
        }

        public static ICommandBuilder RegisterStorageAutoPurgeCommand(this ICommandBuilder builder)
        {
            builder.AddCommand<StorageAutoPurgeCommandInstance, Options>(
                builder =>
                {
                    return new Command("autopurge", "Automatically purge storage consumed by UET if low on disk space.");
                });
            return builder;
        }

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
