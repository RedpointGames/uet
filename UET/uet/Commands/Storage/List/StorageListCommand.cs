namespace UET.Commands.Storage.List
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
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

    internal sealed class StorageListCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<StorageListCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("list", "List the storage consumed by UET and UEFS.");
                })
            .Build();

        internal sealed class Options
        {
            public Option<bool> NoDiskUsage = new Option<bool>("--no-disk-usage", "Skip computing disk usage of folders.");
        }

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern uint GetCompressedFileSize(string lpFileName, out uint lpFileSizeHigh);

        private sealed class StorageListCommandInstance : ICommandInstance
        {
            private readonly ILogger<StorageListCommandInstance> _logger;
            private readonly IStorageManagement _storageManagement;
            private readonly Options _options;

            public StorageListCommandInstance(
                ILogger<StorageListCommandInstance> logger,
                IStorageManagement storageManagement,
                Options options)
            {
                _logger = logger;
                _storageManagement = storageManagement;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var noDiskUsage = context.ParseResult.GetValueForOption(_options.NoDiskUsage);

                var result = await _storageManagement.ListStorageAsync(
                    !noDiskUsage,
                    (int total) =>
                    {
                        Console.WriteLine($"UET is now scanning {total} directories to compute disk space usage...  (this might take a while)");
                    },
                    ((int total, int remaining) info) =>
                    {
                        Console.WriteLine($"UET is now scanning {info.total} directories to compute disk space usage...  ({info.remaining} to go)");
                    },
                    context.GetCancellationToken()).ConfigureAwait(false);

                var now = DateTimeOffset.UtcNow;
                if (result.Entries.Count > 0)
                {
                    if (!noDiskUsage)
                    {
                        Console.WriteLine($"{"Id".PadRight(result.MaxIdLength)} | {"Path".PadRight(result.MaxPathLength)} | {"Type".ToString().PadRight(result.MaxTypeLength)} | Disk Space   | Last Used");
                        foreach (var entry in result.Entries.OrderByDescending(x => x.DiskSpaceConsumed))
                        {
                            var since = now - entry.LastUsed;
                            var sinceString = since.TotalHours > 24
                                ? $"{Math.Ceiling(since.TotalDays)} days ago"
                                : $"{Math.Ceiling(since.TotalHours)} hours ago";
                            switch (entry.Type)
                            {
                                case StorageEntryType.UefsHostPackagesCache:
                                case StorageEntryType.UefsGitSharedIndexCache:
                                case StorageEntryType.UefsGitSharedRepository:
                                case StorageEntryType.UefsGitSharedDependencies:
                                case StorageEntryType.UefsGitSharedBlobs:
                                    sinceString = string.Empty;
                                    break;
                            }

                            Console.WriteLine($"{entry.Id.PadRight(result.MaxIdLength)} | {entry.Path.PadRight(result.MaxPathLength)} | {entry.Type.ToString().PadRight(result.MaxTypeLength)} | {entry.DiskSpaceConsumed / 1024 / 1024,9:F1} MB | {sinceString}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{"Id".PadRight(result.MaxIdLength)} | {"Path".PadRight(result.MaxPathLength)} | {"Type".ToString().PadRight(result.MaxTypeLength)} | Last Used");
                        foreach (var entry in result.Entries.OrderByDescending(x => (now - x.LastUsed).TotalHours))
                        {
                            var since = now - entry.LastUsed;
                            var sinceString = since.TotalHours > 24
                                ? $"{Math.Ceiling(since.TotalDays)} days ago"
                                : $"{Math.Ceiling(since.TotalHours)} hours ago";
                            switch (entry.Type)
                            {
                                case StorageEntryType.UefsHostPackagesCache:
                                case StorageEntryType.UefsGitSharedIndexCache:
                                case StorageEntryType.UefsGitSharedRepository:
                                case StorageEntryType.UefsGitSharedDependencies:
                                case StorageEntryType.UefsGitSharedBlobs:
                                    sinceString = string.Empty;
                                    break;
                            }

                            Console.WriteLine($"{entry.Id.PadRight(result.MaxIdLength)} | {entry.Path.PadRight(result.MaxPathLength)} | {entry.Type.ToString().PadRight(result.MaxTypeLength)} | {sinceString}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("UET is not currently using any storage.");
                }
                return 0;
            }
        }
    }
}
