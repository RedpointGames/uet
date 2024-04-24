namespace UET.Commands.Storage.Purge
{
    using Redpoint.CommandLine;
    using Redpoint.Uet.Workspace.Storage;
    using System.CommandLine;
    using System.Threading.Tasks;

    internal static class StoragePurgeCommand
    {
        internal sealed class Options
        {
            public Option<bool> Force = new Option<bool>("-f", "Actually purge directories instead of doing a dry run.");
            public Option<int> Days = new Option<int>("--days", "The number of days since a storage entry was last used in order for it to be purged. Setting this to 0 will purge everything.") { IsRequired = true };

            public Options()
            {
                Days.AddAlias("-d");
            }
        }

        public static ICommandBuilder RegisterStoragePurgeCommand(this ICommandBuilder builder)
        {
            builder.AddCommand<StoragePurgeCommandInstance, Options>(
                builder =>
                {
                    return new Command("purge", "Purge storage consumed by UET.");
                });
            return builder;
        }

        private sealed class StoragePurgeCommandInstance : ICommandInstance
        {
            private readonly IStorageManagement _storageManagement;
            private readonly Options _options;

            public StoragePurgeCommandInstance(
                IStorageManagement storageManagement,
                Options options)
            {
                _storageManagement = storageManagement;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var force = context.ParseResult.GetValueForOption(_options.Force);
                var days = context.ParseResult.GetValueForOption(_options.Days);

                await _storageManagement.PurgeStorageAsync(
                    force,
                    days,
                    context.GetCancellationToken()).ConfigureAwait(false);

                return 0;
            }
        }
    }
}
