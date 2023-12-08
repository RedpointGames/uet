namespace Redpoint.IO
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using Windows.Win32;
    using Windows.Win32.Storage.FileSystem;

    /// <summary>
    /// Provides APIs for interacting with junctions on Windows.
    /// </summary>
    [SupportedOSPlatform("windows5.1.2600")]
    public static class Junction
    {
        /// <summary>
        /// Creates a junction on Windows. <paramref name="junctionTarget"/> will be resolved to the fully qualified DOS device path automatically.
        /// </summary>
        /// <param name="pathOfJunction">The path that the junction should be created at.</param>
        /// <param name="junctionTarget">The target directory that the junction should point at.</param>
        /// <param name="overwrite">If true and there is an existing junction at <paramref name="pathOfJunction"/>, it will be removed.</param>
        /// <exception cref="ArgumentException">The junction target is not a fully qualified absolute path.</exception>
        /// <exception cref="IOException">A directory already exists at <paramref name="pathOfJunction"/> and <paramref name="overwrite"/> is false.</exception>
        public static void CreateJunction(
            string pathOfJunction,
            string junctionTarget,
            bool overwrite)
        {
            if (!Path.IsPathFullyQualified(junctionTarget))
            {
                throw new ArgumentException("Destination must be an absolute path.");
            }

            if (Directory.Exists(pathOfJunction))
            {
                if (!overwrite)
                {
                    throw new IOException("Directory already exists for junction location.");
                }
            }
            else
            {
                Directory.CreateDirectory(pathOfJunction);
            }

            using (var reparsePointHandle = PInvoke.CreateFile(
                pathOfJunction,
                0x40000000U /* GENERIC_WRITE */,
                FILE_SHARE_MODE.FILE_SHARE_READ
                    | FILE_SHARE_MODE.FILE_SHARE_WRITE
                    | FILE_SHARE_MODE.FILE_SHARE_DELETE,
                null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS
                    | FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OPEN_REPARSE_POINT,
                null))
            {
                if (reparsePointHandle.IsInvalid ||
                    Marshal.GetLastWin32Error() != 0)
                {
                    throw new IOException("Unable to open reparse point.", new Win32Exception());
                }

                var target = DosDevicePath.GetFullyQualifiedDosDevicePath(junctionTarget);
                if (target.Length >= 8192)
                {
                    throw new ArgumentException($"The path '{target}' is too long.");
                }
                var targetByteLength = (ushort)(target.Length * 2);
                var reparseDataBuffer = new REPARSE_DATA_BUFFER
                {
                    ReparseTag = PInvoke.IO_REPARSE_TAG_MOUNT_POINT,
                    ReparseDataLength = (ushort)(targetByteLength + 12),
                    SubstituteNameOffset = 0,
                    SubstituteNameLength = targetByteLength,
                    PrintNameOffset = (ushort)(targetByteLength + 2),
                    PrintNameLength = 0,
                };
                unsafe
                {
                    Marshal.Copy(
                        target.ToCharArray(),
                        0,
                        (nint)reparseDataBuffer.PathBuffer,
                        target.Length);
                };

                uint bytesReturned;
                Windows.Win32.Foundation.BOOL result;
                unsafe
                {
                    result = PInvoke.DeviceIoControl(
                        reparsePointHandle,
                        PInvoke.FSCTL_SET_REPARSE_POINT,
                        &reparseDataBuffer,
                        (uint)(targetByteLength + 20),
                        null,
                        0,
                        &bytesReturned,
                        null);
                }
                if (!result)
                {
                    throw new IOException("Unable to create junction.", new Win32Exception());
                }
            }
        }

        /// <summary>
        /// Creates a junction on Windows, with the raw <paramref name="junctionRawTarget"/> set as the target. You must provide an already valid target for the junction in <paramref name="junctionRawTarget"/> or the junction will not work correctly when applications try to use it.
        /// </summary>
        /// <param name="pathOfJunction">The path that the junction should be created at.</param>
        /// <param name="junctionRawTarget">The raw value that should be set into the junction.</param>
        /// <param name="overwrite">If true and there is an existing junction at <paramref name="pathOfJunction"/>, it will be removed.</param>
        /// <exception cref="ArgumentException">The junction target is not a fully qualified absolute path.</exception>
        /// <exception cref="IOException">A directory already exists at <paramref name="pathOfJunction"/> and <paramref name="overwrite"/> is false.</exception>
        public static void CreateRawJunction(
            string pathOfJunction,
            string junctionRawTarget,
            bool overwrite)
        {
            ArgumentNullException.ThrowIfNull(pathOfJunction);
            ArgumentNullException.ThrowIfNull(junctionRawTarget);

            if (Directory.Exists(pathOfJunction))
            {
                if (!overwrite)
                {
                    throw new IOException("Directory already exists for junction location.");
                }
            }
            else
            {
                Directory.CreateDirectory(pathOfJunction);
            }

            using (var reparsePointHandle = PInvoke.CreateFile(
                pathOfJunction,
                0x40000000U /* GENERIC_WRITE */,
                FILE_SHARE_MODE.FILE_SHARE_READ
                    | FILE_SHARE_MODE.FILE_SHARE_WRITE
                    | FILE_SHARE_MODE.FILE_SHARE_DELETE,
                null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS
                    | FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OPEN_REPARSE_POINT,
                null))
            {
                if (reparsePointHandle.IsInvalid ||
                    Marshal.GetLastWin32Error() != 0)
                {
                    throw new IOException("Unable to open reparse point.", new Win32Exception());
                }

                if (junctionRawTarget.Length >= 8192)
                {
                    throw new ArgumentException($"The path '{junctionRawTarget}' is too long.");
                }
                var targetByteLength = (ushort)(junctionRawTarget.Length * 2);
                var reparseDataBuffer = new REPARSE_DATA_BUFFER
                {
                    ReparseTag = PInvoke.IO_REPARSE_TAG_MOUNT_POINT,
                    ReparseDataLength = (ushort)(targetByteLength + 12),
                    SubstituteNameOffset = 0,
                    SubstituteNameLength = targetByteLength,
                    PrintNameOffset = (ushort)(targetByteLength + 2),
                    PrintNameLength = 0,
                };
                unsafe
                {
                    Marshal.Copy(
                        junctionRawTarget.ToCharArray(),
                        0,
                        (nint)reparseDataBuffer.PathBuffer,
                        junctionRawTarget.Length);
                };

                uint bytesReturned;
                Windows.Win32.Foundation.BOOL result;
                unsafe
                {
                    result = PInvoke.DeviceIoControl(
                        reparsePointHandle,
                        PInvoke.FSCTL_SET_REPARSE_POINT,
                        &reparseDataBuffer,
                        (uint)(targetByteLength + 20),
                        null,
                        0,
                        &bytesReturned,
                        null);
                }
                if (!result)
                {
                    throw new IOException("Unable to create junction.", new Win32Exception());
                }
            }
        }
    }
}
