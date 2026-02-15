namespace Redpoint.Uefs.Package.Vhd
{
    using Microsoft.Win32.SafeHandles;
    using global::Windows.Win32.Storage.Vhd;
    using global::Windows.Win32.Foundation;
    using global::Windows.Win32.Security;
    using DiscUtils.Vhd;
    using global::Windows.Win32;
    using global::Windows.Win32.Storage.FileSystem;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Windows.VolumeManagement;
    using Redpoint.Uefs.Package;
    using System.Globalization;
    using Redpoint.Hashing;
    using System.Text;

    [SupportedOSPlatform("windows6.2")]
    internal sealed class VhdPackageMounter : IPackageMounter
    {
        private readonly ILogger? _logger;
        private SafeFileHandle? _vhd;
        private bool _isAttached;
        private string? _mountPath;
        private string? _writeStoragePath;
        private WriteScratchPersistence _writeScratchPersistence;

        internal static readonly byte[] _magicHeader = new byte[]
        {
            0x63, 0x6F, 0x6E, 0x65,
            0x63, 0x74, 0x69, 0x78
        };

        public static Memory<byte> MagicHeader => _magicHeader;

        public bool RequiresAdminPermissions => true;

        public bool CompatibleWithDocker => true;

        public string WriteStoragePath => _writeStoragePath!;

        private unsafe delegate TReturn DynamicallySizedStringOperation<TReturn>(char* buffer, ref uint bufferSize);

        private unsafe (TReturn, string) GetDynamicallySizedString<TReturn>(DynamicallySizedStringOperation<TReturn> operation)
        {
            uint size = 0;
            TReturn result = operation(null, ref size);
            Span<char> buffer = new char[size / 2];
            string stringValue;
            fixed (char* ptr = buffer)
            {
                result = operation(ptr, ref size);
                stringValue = new string(
                    ptr,
                    0,
                    buffer.Length - 1 /* exclude null terminator */);
            }
            return (result, stringValue);
        }

        public VhdPackageMounter(ILogger? logger)
        {
            _logger = logger;
        }

        public ValueTask MountAsync(
            string packagePath,
            string mountPath,
            string writeStoragePath,
            WriteScratchPersistence persistenceMode)
        {
            if (_vhd != null)
            {
                throw new InvalidOperationException("Mount has already been called!");
            }

            _writeScratchPersistence = persistenceMode;

            if (persistenceMode != WriteScratchPersistence.Keep)
            {
                // Delete the write storage path if it exists.
                if (Directory.Exists(writeStoragePath))
                {
                    Directory.Delete(writeStoragePath, true);
                }
                if (File.Exists(writeStoragePath))
                {
                    File.Delete(writeStoragePath);
                }
            }

            // Delete the mount path (non-recursively) if it exists.
            if (Directory.Exists(mountPath))
            {
                Directory.Delete(mountPath);
            }
            Directory.CreateDirectory(mountPath);
            _mountPath = mountPath;

            // Create the write storage path as a directory.
            Directory.CreateDirectory(writeStoragePath);
            _writeStoragePath = writeStoragePath;

            // Get the package path, and the last modified time of the path, to make a unique hash for the shadow path. This prevents us from re-using a shadow path when either the base package changes or the file is written to.
            var hash = Hash.XxHash64(
                $"{File.GetLastWriteTimeUtc(packagePath).Ticks}-{packagePath}",
                Encoding.UTF8);

            // Create a differencing disk.
            var differencingDiskPath = Path.Combine(writeStoragePath, $"writelayer-{hash.Hash}-{(persistenceMode == WriteScratchPersistence.Keep ? "keep" : "discard")}.vhd");
            if (!File.Exists(differencingDiskPath) || persistenceMode != WriteScratchPersistence.Keep)
            {
                var differencingDisk = Disk.InitializeDifferencing(differencingDiskPath, packagePath);
                differencingDisk.Dispose();
            }

            unsafe
            {
                // Open the VHD.
                _logger?.LogTrace($"Opening VHD located at: '{differencingDiskPath}'...");
                var storageType = new VIRTUAL_STORAGE_TYPE
                {
                    DeviceId = PInvoke.VIRTUAL_STORAGE_TYPE_DEVICE_UNKNOWN,
                    VendorId = PInvoke.VIRTUAL_STORAGE_TYPE_VENDOR_UNKNOWN,
                };
                var openParameters = new OPEN_VIRTUAL_DISK_PARAMETERS
                {
                    Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_2,
                    Anonymous = new OPEN_VIRTUAL_DISK_PARAMETERS._Anonymous_e__Union
                    {
                        Version2 = new OPEN_VIRTUAL_DISK_PARAMETERS._Anonymous_e__Union._Version2_e__Struct
                        {
                            GetInfoOnly = false,
                        }
                    }
                };
                var result = PInvoke.OpenVirtualDisk(
                    storageType,
                    differencingDiskPath,
                    VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_NONE,
                    OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                    openParameters,
                    out _vhd);
                if (result != WIN32_ERROR.ERROR_SUCCESS)
                {
                    throw new InvalidOperationException($"Failed to open virtual disk: {result}");
                }

                // Parse the security descriptor for attaching the disk.
                _logger?.LogTrace($"Parsing security descriptor...");
                PSECURITY_DESCRIPTOR sd;
                if (!PInvoke.ConvertStringSecurityDescriptorToSecurityDescriptor(
                    "O:BAG:BAD:(A;;GA;;;WD)",
                    PInvoke.SDDL_REVISION_1,
                    out sd,
                    out _))
                {
                    throw new InvalidOperationException("Unable to parse security descriptor!");
                }

                // Attach the VHD (but with no drive letter).
                _logger?.LogTrace($"Attaching VHD with no drive letter...");
                var attachParameters = new ATTACH_VIRTUAL_DISK_PARAMETERS
                {
                    Version = ATTACH_VIRTUAL_DISK_VERSION.ATTACH_VIRTUAL_DISK_VERSION_1,
                    Anonymous = new ATTACH_VIRTUAL_DISK_PARAMETERS._Anonymous_e__Union
                    {
                        Version1 = new ATTACH_VIRTUAL_DISK_PARAMETERS._Anonymous_e__Union._Version1_e__Struct
                        {
                        }
                    }
                };
                var attachAttempts = 0;
            retryAttach:
                result = PInvoke.AttachVirtualDisk(
                    _vhd,
                    new PSECURITY_DESCRIPTOR(null),
                    ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER,
                    0,
                    attachParameters,
                    null);
                if (result == WIN32_ERROR.ERROR_SHARING_VIOLATION && attachAttempts < 10)
                {
                    _logger?.LogWarning("Attempting to retry AttachVirtualDisk operation; got sharing violation error!");
                    attachAttempts++;
                    Thread.Sleep(1000);
                    goto retryAttach;
                }
                if (result != WIN32_ERROR.ERROR_SUCCESS)
                {
                    throw new InvalidOperationException($"Failed to attach virtual disk: {result}");
                }
                _isAttached = true;

                // Get the physical drive path of the attached disk.
                _logger?.LogTrace($"Retrieving physical drive path of attached disk...");
                var (getDiskResult, physicalPathStr) = GetDynamicallySizedString((char* buffer, ref uint bufferSize) =>
                {
                    fixed (uint* bufferSizePtr = &bufferSize)
                    {
                        return PInvoke.GetVirtualDiskPhysicalPath(
                            (HANDLE)_vhd.DangerousGetHandle(),
                            bufferSizePtr,
                            (PWSTR)buffer);
                    }
                });
                if (getDiskResult != WIN32_ERROR.ERROR_SUCCESS)
                {
                    throw new InvalidOperationException($"Failed to physical path of attached disk: {getDiskResult}");
                }
                var physicalPathDiskNumber = uint.Parse(physicalPathStr["\\\\.\\PhysicalDrive".Length..], CultureInfo.InvariantCulture);

                // Iterate through all of the volumes on the system until we find the one
                // that matches our physical disk number.
                _logger?.LogTrace($"Iterating through volumes of attached disk...");
                var didMount = false;
                foreach (var volume in new SystemVolumes())
                {
                    // Open the current volume.
                    var hasTrailingSlash = false;
                    var volumeName = volume.VolumeName;
                    if (volumeName.EndsWith(Path.DirectorySeparatorChar))
                    {
                        hasTrailingSlash = true;
                        volumeName = volumeName.TrimEnd(Path.DirectorySeparatorChar);
                    }
                    using (var volumeHandle = PInvoke.CreateFile(
                        volumeName,
                        0,
                        FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
                        null,
                        FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                        0,
                        null))
                    {
                        if (volumeHandle.IsInvalid)
                        {
                            throw new InvalidOperationException($"Failed to open volume: {volumeName} (0x{Convert.ToString(Marshal.GetLastWin32Error(), 16).PadLeft(8, '0')})");
                        }

                        // Get the volume extents.
                        global::Windows.Win32.System.Ioctl.VOLUME_DISK_EXTENTS extents;
                        if (!PInvoke.DeviceIoControl(
                            (HANDLE)volumeHandle.DangerousGetHandle(),
                            PInvoke.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
                            null,
                            0,
                            &extents,
                            (uint)sizeof(global::Windows.Win32.System.Ioctl.VOLUME_DISK_EXTENTS),
                            null,
                            null))
                        {
                            throw new InvalidOperationException($"Failed to get volume extents: {volumeName} (0x{Convert.ToString(Marshal.GetLastWin32Error(), 16).PadLeft(8, '0')})");
                        }

                        // Is this the volume we're interested in?
                        if (extents.Extents.e0.DiskNumber == physicalPathDiskNumber)
                        {
                            if (hasTrailingSlash)
                            {
                                volumeName += "\\";
                            }

                            _logger?.LogTrace($"Setting volume mount point for discovered volume...");
                            if (!PInvoke.SetVolumeMountPoint(mountPath.TrimEnd('\\') + '\\', volumeName))
                            {
                                throw new InvalidOperationException($"Failed to mount volume: {volumeName} (0x{Convert.ToString(Marshal.GetLastWin32Error(), 16).PadLeft(8, '0')})");
                            }

                            // We only want to mount one volume.
                            didMount = true;
                            break;
                        }
                    }
                }
                if (!didMount)
                {
                    throw new InvalidOperationException("Unable to find volume in attached disk!");
                }
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (_isAttached)
            {
                _logger?.LogInformation($"Detaching VHD...");
                unsafe
                {
                    // Detach the VHD
                    var result = PInvoke.DetachVirtualDisk(
                        _vhd,
                        DETACH_VIRTUAL_DISK_FLAG.DETACH_VIRTUAL_DISK_FLAG_NONE,
                        0);
                    if (result != WIN32_ERROR.ERROR_SUCCESS &&
                        result != WIN32_ERROR.ERROR_NOT_READY /* No longer mounted at time of unmount */)
                    {
                        throw new InvalidOperationException($"Failed to detach virtual disk: {result}");
                    }
                    _isAttached = false;
                    if (Directory.Exists(_mountPath))
                    {
                        Directory.Delete(_mountPath);
                    }
                }

                if (_writeScratchPersistence != WriteScratchPersistence.Keep)
                {
                    _logger?.LogInformation($"Cleaning up temporary write layer for VHD...");
                    for (int i = 0; i < 10; i++)
                    {
                        try
                        {
                            // Clean up the write storage path as well.
                            Directory.Delete(_writeStoragePath!, true);
                            break;
                        }
                        catch (IOException ex)
                        {
                            // Try again in a moment.
                            Thread.Sleep(i * 100);

                            _logger?.LogWarning($"Unable to clean up temporary write layer for VHD: {ex.Message}");
                        }
                    }
                }
            }
            if (_vhd != null)
            {
                // Close the VHD handle.
                _vhd.Dispose();
                _vhd = null;
            }
            return ValueTask.CompletedTask;
        }

        public Task<(string packagePath, string mountPath, IPackageMounter mounter)[]> ImportExistingMountsAtStartupAsync()
        {
            // @todo: Not yet implemented on Windows.
            return Task.FromResult(Array.Empty<(string packagePath, string mountPath, IPackageMounter mounter)>());
        }
    }
}
