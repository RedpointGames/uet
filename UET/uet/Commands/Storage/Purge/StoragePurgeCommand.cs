namespace UET.Commands.Storage.Purge
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.Uet.Workspace.Reservation;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;
    using UET.Commands.Build;

    internal class StoragePurgeCommand
    {
        internal class Options
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

        private class StoragePurgeCommandInstance : ICommandInstance
        {
            private readonly ILogger<StoragePurgeCommandInstance> _logger;
            private readonly IReservationManagerForUet _reservationManager;
            private readonly Options _options;
            private readonly string _reservationManagerRootPath;

            public StoragePurgeCommandInstance(
                ILogger<StoragePurgeCommandInstance> logger,
                IReservationManagerForUet reservationManager,
                Options options)
            {
                _logger = logger;
                _reservationManager = reservationManager;
                _options = options;
                if (OperatingSystem.IsWindows())
                {
                    _reservationManagerRootPath = Path.Combine($"{Environment.GetEnvironmentVariable("SYSTEMDRIVE")}\\", "UES");
                }
                else if (OperatingSystem.IsMacOS())
                {
                    _reservationManagerRootPath = "/Users/Shared/.ues";
                }
                else
                {
                    _reservationManagerRootPath = "/tmp/.ues";
                }
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var force = context.ParseResult.GetValueForOption(_options.Force);
                var days = context.ParseResult.GetValueForOption(_options.Days);

                var scan = new List<(DirectoryInfo dir, FileInfo? metaFile, StorageEntryType type)>();
                var reservationRoot = new DirectoryInfo(_reservationManagerRootPath);
                if (reservationRoot.Exists)
                {
                    scan.AddRange(reservationRoot.GetDirectories()
                        .Where(x => x.Name != ".lock" && x.Name != ".meta")
                        .Select(x => (
                            x,
                            (FileInfo?)new FileInfo(Path.Combine(reservationRoot.FullName, ".meta", "date." + x.Name)),
                            StorageEntryType.Generic)));
                }
                var remaining = scan.Count;

                var now = DateTimeOffset.UtcNow;
                var hasAnyToRemove = false;

                var entries = new List<StorageEntry>();
                var semaphore = new SemaphoreSlim(1);
                await Parallel.ForEachAsync(
                    scan.ToAsyncEnumerable(),
                    context.GetCancellationToken(),
                    async (entry, ct) =>
                    {
                        var directory = entry.dir;
                        if (!directory.Exists)
                        {
                            return;
                        }

                        DateTimeOffset lastUsed = directory.LastWriteTimeUtc;
                        if (entry.metaFile != null && entry.metaFile.Exists)
                        {
                            lastUsed = DateTimeOffset.FromUnixTimeSeconds(long.Parse(File.ReadAllText(entry.metaFile.FullName).Trim()));
                        }

                        var lastUsedDays = now - lastUsed;
                        if (Math.Ceiling(lastUsedDays.TotalDays) >= days)
                        {
                            hasAnyToRemove = true;
                            if (force)
                            {
                                await using (var reservation = (await _reservationManager.ReserveExactAsync(directory.Name, context.GetCancellationToken()).ConfigureAwait(false)).ConfigureAwait(false))
                                {
                                    try
                                    {
                                        _logger.LogInformation($"Removing directory '{directory.FullName}'...");
                                        await DirectoryAsync.DeleteAsync(directory.FullName, true).ConfigureAwait(false);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning($"Failed to delete directory '{directory.FullName}': {ex}");
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogInformation($"Would remove directory '{directory.FullName}' as it is {lastUsedDays.TotalDays:F0} days old.");
                            }
                        }
                        return;
                    }).ConfigureAwait(false);

                if (!hasAnyToRemove)
                {
                    _logger.LogInformation("No storage used by UET met the threshold for removal.");
                }

                return 0;
            }
        }
    }
}
