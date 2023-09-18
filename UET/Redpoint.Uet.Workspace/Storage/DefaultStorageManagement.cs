namespace Redpoint.Uet.Workspace.Storage
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.Uet.Workspace.Reservation;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading.Tasks;
    using Redpoint.Concurrency;

    internal class DefaultStorageManagement : IStorageManagement
    {
        private readonly string _reservationManagerRootPath;
        private readonly string? _uefsStoragePath;
        private readonly ILogger<DefaultStorageManagement> _logger;
        private readonly IReservationManagerForUet _reservationManager;

        public DefaultStorageManagement(
            ILogger<DefaultStorageManagement> logger,
            IReservationManagerForUet reservationManager)
        {
            _reservationManagerRootPath = reservationManager.RootPath;
            if (OperatingSystem.IsWindows())
            {
                _uefsStoragePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "UEFS");
            }
            else if (OperatingSystem.IsMacOS())
            {
                _uefsStoragePath = Path.Combine("/Users", "Shared", "UEFS");
            }
            _logger = logger;
            _reservationManager = reservationManager;
        }

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern uint GetCompressedFileSize(string lpFileName, out uint lpFileSizeHigh);

        public async Task<ListStorageResult> ListStorageAsync(
            bool includeDiskUsage,
            Action<int> onStart,
            Action<(int total, int remaining)> onProgress,
            CancellationToken cancellationToken)
        {
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

            if (scan.Count > 0 && includeDiskUsage)
            {
                onStart(scan.Count);
            }

            var maxIdLength = 0;
            var maxPathLength = 0;
            var maxTypeLength = 0;
            var remaining = scan.Count;

            void EmitRemaining()
            {
                if (includeDiskUsage)
                {
                    onProgress((scan!.Count, remaining));
                }
            }

            var entries = new List<StorageEntry>();
            var semaphore = new SemaphoreSlim(1);
            await Parallel.ForEachAsync(
                scan.ToAsyncEnumerable(),
                cancellationToken,
                async (entry, ct) =>
                {
                    var directory = entry.dir;
                    if (!directory.Exists)
                    {
                        await semaphore.WaitAsync(ct).ConfigureAwait(false);
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
                    if (includeDiskUsage)
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
                        lastUsed = DateTimeOffset.FromUnixTimeSeconds(long.Parse(File.ReadAllText(entry.metaFile.FullName).Trim(), CultureInfo.InvariantCulture));
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

                    await semaphore.WaitAsync(ct).ConfigureAwait(false);
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
                }).ConfigureAwait(false);

            return new ListStorageResult
            {
                Entries = entries,
                MaxIdLength = maxIdLength,
                MaxPathLength = maxPathLength,
                MaxTypeLength = maxTypeLength,
            };
        }

        public async Task PurgeStorageAsync(
            bool performDeletion,
            int daysThreshold,
            CancellationToken cancellationToken)
        {
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
                cancellationToken,
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
                        lastUsed = DateTimeOffset.FromUnixTimeSeconds(long.Parse(File.ReadAllText(entry.metaFile.FullName).Trim(), CultureInfo.InvariantCulture));
                    }

                    var lastUsedDays = now - lastUsed;
                    if (Math.Ceiling(lastUsedDays.TotalDays) >= daysThreshold)
                    {
                        hasAnyToRemove = true;
                        if (performDeletion)
                        {
                            await using ((await _reservationManager.ReserveExactAsync(directory.Name, cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var reservation).ConfigureAwait(false))
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
        }

        public async Task AutoPurgeStorageAsync(
            CancellationToken cancellationToken)
        {
            const long fixedBytesThreshold = 512 * 1024 * 1024L;
            const double percentThreshold = 0.20f;

            for (int daysThreshold = 7; daysThreshold >= 1; daysThreshold--)
            {
                long diskSpaceUsedBytes;
                long diskSpaceTotalBytes;
                try
                {
                    var drive = new DriveInfo(Path.GetFullPath(_reservationManagerRootPath));
                    diskSpaceUsedBytes = drive.AvailableFreeSpace;
                    diskSpaceTotalBytes = drive.TotalSize;
                }
                catch
                {
                    _logger.LogWarning($"Unable to check used disk space for reservation root path: {_reservationManagerRootPath}");
                    return;
                }

                if ((diskSpaceTotalBytes - diskSpaceUsedBytes) < fixedBytesThreshold)
                {
                    _logger.LogInformation($"Performing automatic storage cleanup for data older than {daysThreshold} days, as the the reservation path has less than {fixedBytesThreshold / 1024 / 1024}MB of disk space left.");
                }
                else if (diskSpaceUsedBytes / (double)diskSpaceTotalBytes > (1.0 - percentThreshold))
                {
                    _logger.LogInformation($"Performing automatic storage cleanup for data older than {daysThreshold} days, as the the reservation path has less than {percentThreshold * 100}% of disk space left.");
                }
                else
                {
                    return;
                }

                try
                {
                    await PurgeStorageAsync(
                        true,
                        daysThreshold,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to automatically cleanup storage: {ex}");
                    return;
                }
            }
        }
    }
}
