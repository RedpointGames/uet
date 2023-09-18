namespace UET.Commands.Storage.Purge
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.Uet.Workspace.Reservation;
    using Redpoint.Uet.Workspace.Storage;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;
    using UET.Commands.Build;
    using UET.Services;

    internal sealed class StoragePurgeCommand
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

        public static Command CreatePurgeCommand()
        {
            var command = new Command("purge", "Purge storage consumed by UET.");
            command.AddServicedOptionsHandler<StoragePurgeCommandInstance, Options>();
            return command;
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

            public async Task<int> ExecuteAsync(InvocationContext context)
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
