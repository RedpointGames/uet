namespace UET.Commands.Storage.List
{
    using Microsoft.Extensions.Logging;
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

    internal class StorageListCommand
    {
        internal class Options
        {
            public Option<bool> NoDiskUsage = new Option<bool>("--no-disk-usage", "Skip computing disk usage of folders.");
        }

        public static Command CreateListCommand()
        {
            var command = new Command("list", "List the storage consumed by UET and UEFS.");
            command.AddServicedOptionsHandler<StorageListCommandInstance, Options>();
            return command;
        }

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll")]
        static extern uint GetCompressedFileSize(string lpFileName, out uint lpFileSizeHigh);

        private class StorageListCommandInstance : ICommandInstance
        {
            private readonly ILogger<StorageListCommandInstance> _logger;
            private readonly IReservationManagerForUet _reservationManager;
            private readonly Options _options;
            private readonly string _reservationManagerRootPath;
            private readonly string? _uefsStoragePath;

            public StorageListCommandInstance(
                ILogger<StorageListCommandInstance> logger,
                IReservationManagerForUet reservationManager,
                Options options)
            {
                _logger = logger;
                _reservationManager = reservationManager;
                _options = options;
                if (OperatingSystem.IsWindows())
                {
                    _reservationManagerRootPath = Path.Combine($"{Environment.GetEnvironmentVariable("SYSTEMDRIVE")}\\", "UES");
                    _uefsStoragePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "UEFS");
                }
                else if (OperatingSystem.IsMacOS())
                {
                    _reservationManagerRootPath = "/Users/Shared/.ues";
                    _uefsStoragePath = Path.Combine("/Users", "Shared", "UEFS");
                }
                else
                {
                    _reservationManagerRootPath = "/tmp/.ues";
                }
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var noDiskUsage = context.ParseResult.GetValueForOption(_options.NoDiskUsage);

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
                if (_uefsStoragePath != null && Directory.Exists(_uefsStoragePath))
                {
                    scan.AddRange(new[]
                    {
                        (new DirectoryInfo(Path.Combine(_uefsStoragePath, "git-blob")), (FileInfo?)null, StorageEntryType.UefsGitSharedBlobs),
                        (new DirectoryInfo(Path.Combine(_uefsStoragePath, "git-deps")), null, StorageEntryType.UefsGitSharedDependencies),
                        (new DirectoryInfo(Path.Combine(_uefsStoragePath, "git-index-cache")), null,  StorageEntryType.UefsGitSharedIndexCache),
                        (new DirectoryInfo(Path.Combine(_uefsStoragePath, "git-repo")), null, StorageEntryType.UefsGitSharedRepository),
                        (new DirectoryInfo(Path.Combine(_uefsStoragePath, "hostpkgs", "cache")), null, StorageEntryType.UefsHostPackagesCache),
                    });
                }

                if (scan.Count > 0 && !noDiskUsage)
                {
                    Console.WriteLine($"UET is now scanning {scan.Count} directories to compute disk space usage...  (this might take a while)");
                }

                var maxIdLength = 0;
                var maxPathLength = 0;
                var maxTypeLength = 0;
                var remaining = scan.Count;

                void EmitRemaining()
                {
                    if (!noDiskUsage)
                    {
                        Console.WriteLine($"UET is now scanning {scan!.Count} directories to compute disk space usage...  ({remaining} to go)");
                    }
                }

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
                            await semaphore.WaitAsync();
                            try
                            {
                                remaining--;
                                EmitRemaining();
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                            return;
                        }

                        ulong size = 0;
                        if (!noDiskUsage)
                        {
                            try
                            {
                                if (OperatingSystem.IsWindows())
                                {
                                    foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
                                    {
                                        if (file.Attributes.HasFlag(FileAttributes.SparseFile))
                                        {
                                            uint high;
                                            uint low;
                                            low = GetCompressedFileSize(file.FullName, out high);
                                            if (low != 0xFFFFFFFF)
                                            {
                                                size += ((ulong)high << 32) + low;
                                            }
                                        }
                                        else
                                        {
                                            size += (ulong)file.Length;
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
                                    {
                                        size += (ulong)file.Length;
                                    }
                                }
                            }
                            catch (DirectoryNotFoundException)
                            {
                                size = ulong.MaxValue;
                            }
                            catch (UnauthorizedAccessException)
                            {
                                size = ulong.MaxValue;
                            }
                        }

                        var type = entry.type;
                        if (type == StorageEntryType.Generic)
                        {
                            if (File.Exists(Path.Combine(directory.FullName, ".console-zip-extracted")))
                            {
                                type = StorageEntryType.ExtractedConsoleZip;
                            }
                            if (Directory.Exists(Path.Combine(directory.FullName, ".uefs.db")))
                            {
                                type = StorageEntryType.WriteScratchLayer;
                            }
                        }

                        DateTimeOffset lastUsed = directory.LastWriteTimeUtc;
                        if (entry.metaFile != null && entry.metaFile.Exists)
                        {
                            lastUsed = DateTimeOffset.FromUnixTimeSeconds(long.Parse(File.ReadAllText(entry.metaFile.FullName).Trim()));
                        }

                        switch (type)
                        {
                            case StorageEntryType.UefsHostPackagesCache:
                            case StorageEntryType.UefsGitSharedIndexCache:
                            case StorageEntryType.UefsGitSharedRepository:
                            case StorageEntryType.UefsGitSharedDependencies:
                            case StorageEntryType.UefsGitSharedBlobs:
                                lastUsed = DateTimeOffset.MaxValue;
                                break;
                        }

                        await semaphore.WaitAsync();
                        try
                        {
                            maxIdLength = Math.Max(maxIdLength, directory.Name.Length);
                            maxPathLength = Math.Max(maxPathLength, directory.FullName.Length);
                            maxTypeLength = Math.Max(maxTypeLength, type.ToString().Length);

                            entries.Add(new StorageEntry
                            {
                                Id = directory.Name,
                                Path = directory.FullName,
                                Type = type,
                                DiskSpaceConsumed = size,
                                LastUsed = lastUsed,
                            });
                            remaining--;
                            EmitRemaining();
                        }
                        finally
                        {
                            semaphore.Release();
                        }

                        return;
                    });

                var now = DateTimeOffset.UtcNow;
                if (entries.Count > 0)
                {
                    if (!noDiskUsage)
                    {
                        Console.WriteLine($"{"Id".PadRight(maxIdLength)} | {"Path".PadRight(maxPathLength)} | {"Type".ToString().PadRight(maxTypeLength)} | Disk Space   | Last Used");
                        foreach (var entry in entries.OrderByDescending(x => x.DiskSpaceConsumed))
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

                            Console.WriteLine($"{entry.Id.PadRight(maxIdLength)} | {entry.Path.PadRight(maxPathLength)} | {entry.Type.ToString().PadRight(maxTypeLength)} | {entry.DiskSpaceConsumed / 1024 / 1024,9:F1} MB | {sinceString}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{"Id".PadRight(maxIdLength)} | {"Path".PadRight(maxPathLength)} | {"Type".ToString().PadRight(maxTypeLength)} | Last Used");
                        foreach (var entry in entries.OrderByDescending(x => (now - x.LastUsed).TotalHours))
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

                            Console.WriteLine($"{entry.Id.PadRight(maxIdLength)} | {entry.Path.PadRight(maxPathLength)} | {entry.Type.ToString().PadRight(maxTypeLength)} | {sinceString}");
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
