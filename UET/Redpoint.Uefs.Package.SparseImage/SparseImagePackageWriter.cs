using System;

namespace Redpoint.Uefs.Package.SparseImage
{
    using System.Diagnostics;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("macos")]
    internal sealed class SparseImagePackageWriter : IPackageWriter
    {
        private string? _mountPath = null;
        private bool _isMounted = false;

        /// <summary>
        /// HFS+ and other overheads means we can't actually have a tiny disk. Give us
        /// a reasonable minimum.
        /// </summary>
        public long MinimumDiskSize = 64L * 1024L * 1024L;

        // Allowance for filesystem overhead.
        public long IndexHeaderSize => 5L * 1024L * 1024L * 1024L;

        public SparseImagePackageWriter()
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
            // Check the filename is correct for usage with hdiutil.
            if (!packagePath.EndsWith(".sparseimage"))
            {
                throw new PackageWriterException("the package path must end in '.sparseimage'");
            }

            // Make the disk twice as big. This doesn't actually consume space because it's a
            // dynamically expanding disk. Then we ensure it's a suitable power of two.
            var diskSize = dataSize * 2;
            diskSize = (long)Math.Ceiling(diskSize / 1024 / (decimal)1024) * 1024 * 1024;
            if (diskSize < MinimumDiskSize)
            {
                diskSize = MinimumDiskSize;
            }

            // Convert to megabytes for hdiutil.
            var diskSizeMb = (long)Math.Ceiling((decimal)diskSize / 1024 / 1024);
            diskSize = diskSizeMb * 1024 * 1024;

            // Sparse images larger than this size can't
            // be mounted with -shadow, so prevent us from
            // accidentally creating an image that's too big
            // to mount later.
            if (diskSize / 512 >= 17179869184)
            {
                throw new PackageWriterException("macos does not support mounting packages 8TB in size or larger at runtime. preventing you from creating a package that can't be mounted later by refusing to run.");
            }

            // Create the sparse image on disk.
            {
                if (File.Exists(packagePath))
                {
                    File.Delete(packagePath);
                }
                var processInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/hdiutil",
                    ArgumentList = {
                        "create",
                        "-megabytes",
                        $"{diskSizeMb}",
                        "-fs",
                        "APFS",
                        "-type",
                        "SPARSE",
                        "-volname",
                        "UEFS",
                        packagePath
                    },
                    UseShellExecute = false,
                    CreateNoWindow = false,
                };
                var process = new Process();
                process.StartInfo = processInfo;
                process.EnableRaisingEvents = true;
                if (!process.Start())
                {
                    throw new PackageWriterException("unable to start hdiutil process for creating sparse APFS image");
                }
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new PackageWriterException($"hdiutil for creation exited with code {process.ExitCode}");
                }
            }

            // Create a temporary folder we can mount our new image to so that we can start writing.
            _mountPath = Path.Combine(Path.GetTempPath(), "uefsw_" + Guid.NewGuid().ToString());
            while (Directory.Exists(_mountPath))
            {
                _mountPath = Path.Combine(Path.GetTempPath(), "uefsw_" + Guid.NewGuid().ToString());
            }
            Directory.CreateDirectory(_mountPath);

            // Mount our new package at this path.
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/hdiutil",
                    ArgumentList = {
                        "mount",
                        packagePath,
                        "-mountpoint",
                        _mountPath,
                    },
                    UseShellExecute = false,
                    CreateNoWindow = false,
                };
                var process = new Process();
                process.StartInfo = processInfo;
                process.EnableRaisingEvents = true;
                if (!process.Start())
                {
                    throw new PackageWriterException("unable to start hdiutil process for mounting new sparse APFS image");
                }
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new PackageWriterException($"hdiutil for mounting exited with code {process.ExitCode}");
                }
                _isMounted = true;
            }
        }

        public bool WantsDirectoryWrites => true;

        public bool SupportsParallelDirectoryWrites => true;

        public bool SupportsParallelWrites => true;

        public void Dispose()
        {
            if (_isMounted)
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/hdiutil",
                    ArgumentList = {
                        // note: 'unmount' doesn't detach the 
                        // disk and leaves it locked, so we use 'detach'
                        "detach",
                        _mountPath!,
                        "-force"
                    },
                    UseShellExecute = false,
                    CreateNoWindow = false,
                };
                var process = new Process();
                process.StartInfo = processInfo;
                process.EnableRaisingEvents = true;
                if (!process.Start())
                {
                    throw new PackageWriterException("unable to start hdiutil process for unmounting sparse APFS image");
                }
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new PackageWriterException($"hdiutil for unmounting exited with code {process.ExitCode}");
                }
                _isMounted = false;
            }
        }

        public ValueTask WritePackageDirectory(PackageManifestEntry packageManifestEntry, OnFileWriteComplete onDirectoryWriteComplete)
        {
            Directory.CreateDirectory(Path.Combine(_mountPath!, packageManifestEntry.PathInPackage));
            onDirectoryWriteComplete(packageManifestEntry.PathInPackage);
            return ValueTask.CompletedTask;
        }

        public async ValueTask WritePackageFile(PackageManifestEntry packageManifestEntry, OnFileBytesWritten onFileBytesWritten, OnFileWriteComplete onFileWriteComplete)
        {
            using (var reader = new FileStream(packageManifestEntry.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var writer = new FileStream(Path.Combine(_mountPath!, packageManifestEntry.PathInPackage), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[128 * 1024];
                    int bytesRead;
                    while ((bytesRead = await reader.ReadAsync(buffer).ConfigureAwait(false)) != 0)
                    {
                        await writer.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
                        onFileBytesWritten(bytesRead);
                    }

                    onFileWriteComplete(packageManifestEntry.PathInPackage);
                }
            }
            File.SetUnixFileMode(
                Path.Combine(_mountPath!, packageManifestEntry.PathInPackage),
                File.GetUnixFileMode(packageManifestEntry.SourcePath));
        }

        public ValueTask WritePackageIndex(PackageManifest packageManifest)
        {
            return ValueTask.CompletedTask;
        }
    }
}
