namespace Redpoint.Uefs.Package.Vhd
{
    using DiscUtils;
    using DiscUtils.Ntfs;
    using DiscUtils.Partitions;
    using DiscUtils.Streams;
    using DiscUtils.Vhd;
    using Redpoint.Uefs.Package;
    using System.Runtime.Versioning;
    using System.Security.AccessControl;

    [SupportedOSPlatform("windows6.2")]
    internal sealed class VhdPackageWriter : IPackageWriter
    {
        private FileStream? _fs;
        private Disk? _disk;
        private NtfsFileSystem? _ntfs;
        private SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// NTFS and other overheads means we can't actually have a tiny disk. Give us
        /// a reasonable minimum. The package won't actually be this big on disk due
        /// to dynamic sizing; it just means we won't try to make a disk that's like
        /// 2MB in size.
        /// </summary>
        public long MinimumDiskSize = 64L * 1024L * 1024L;

        // Add some working space for NTFS, in case the user wants to mount
        // the package read-write in Docker. This won't actually consume space
        // on disk because it's a dynamically expanding disk.
        public long IndexHeaderSize => 5L * 1024L * 1024L * 1024L;

        public VhdPackageWriter()
        {
        }

        public long ComputeEntryIndexSize(string pathInPackage)
        {
            return 0;
        }

        public long ComputeEntryDataSize(string pathInPackage, long size)
        {
            return size;
        }

        public void OpenPackageForWriting(string packagePath, long indexSize, long dataSize)
        {
            // Create the dynamically expanding VHD with an NTFS partition.
            _fs = new FileStream(packagePath, FileMode.Create);

            // Make the disk twice as big. This doesn't actually consume space because it's a
            // dynamically expanding disk. Then we ensure it's a suitable power of two.
            var diskSize = dataSize * 2;
            diskSize = (long)Math.Ceiling(diskSize / 1024 / (decimal)1024) * 1024 * 1024;
            if (diskSize < MinimumDiskSize)
            {
                diskSize = MinimumDiskSize;
            }

            Console.WriteLine($"set VHD maximum disk size to {diskSize / 1024 / 1024} MB");
            _disk = Disk.InitializeDynamic(_fs, Ownership.None, diskSize);

            BiosPartitionTable.Initialize(_disk, WellKnownPartitionType.WindowsNtfs);
            var volMgr = new VolumeManager(_disk);

            _ntfs = NtfsFileSystem.Format(volMgr.GetLogicalVolumes()[0], "UEFS", new NtfsFormatOptions());
            _ntfs.NtfsOptions.ShortNameCreation = ShortFileNameOption.Disabled;
        }

        public void Dispose()
        {
            if (_ntfs != null)
            {
                _ntfs.Dispose();
                _ntfs = null;
            }
            if (_disk != null)
            {
                _disk.Dispose();
                _disk = null;
            }
            if (_fs != null)
            {
                _fs.Flush();
                _fs.Dispose();
                _fs = null;
            }
        }

        public bool WantsDirectoryWrites => true;

        public bool SupportsParallelDirectoryWrites => false;

        public bool SupportsParallelWrites => true;

        // Owned by Everyone, Full Control by Everyone.
        private static readonly RawSecurityDescriptor _securityDescriptor = new RawSecurityDescriptor("O:WDG:WDD:PAI(A;;FA;;;WD)");

        public ValueTask WritePackageDirectory(PackageManifestEntry packageManifestEntry, OnFileWriteComplete onDirectoryWriteComplete)
        {
            _ntfs!.CreateDirectory(packageManifestEntry.PathInPackage, new NewFileOptions
            {
                SecurityDescriptor = _securityDescriptor
            });
            onDirectoryWriteComplete(packageManifestEntry.PathInPackage);
            return ValueTask.CompletedTask;
        }

        public async ValueTask WritePackageFile(PackageManifestEntry packageManifestEntry, OnFileBytesWritten onFileBytesWritten, OnFileWriteComplete onFileWriteComplete)
        {
            // Using a semaphore provides marginally better results, about 30 seconds off
            // packaging an Unreal Engine installation from 10:10 down to 9:40.

            using (var reader = new FileStream(packageManifestEntry.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                SparseStream? writer = null;
                try
                {
                    await _writeSemaphore.WaitAsync();
                    try
                    {
                        writer = _ntfs!.OpenFile(packageManifestEntry.PathInPackage, FileMode.Create, FileAccess.ReadWrite, new NewFileOptions
                        {
                            SecurityDescriptor = _securityDescriptor
                        });
                    }
                    finally
                    {
                        _writeSemaphore.Release();
                    }

                    byte[] buffer = new byte[128 * 1024];
                    int bytesRead;
                    while ((bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
                    {
                        await _writeSemaphore.WaitAsync();
                        try
                        {
                            await writer.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                        }
                        finally
                        {
                            _writeSemaphore.Release();
                        }
                        onFileBytesWritten(bytesRead);
                    }

                    onFileWriteComplete(packageManifestEntry.PathInPackage);
                }
                finally
                {
                    if (writer != null)
                    {
                        await _writeSemaphore.WaitAsync();
                        try
                        {
                            writer.Dispose();
                        }
                        finally
                        {
                            _writeSemaphore.Release();
                        }
                    }
                }
            }
        }

        public ValueTask WritePackageIndex(PackageManifest packageManifest)
        {
            return ValueTask.CompletedTask;
        }
    }
}
