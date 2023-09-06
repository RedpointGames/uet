namespace Redpoint.Rfs.WinFsp
{
    using System;
    using System.Collections;
    using System.Runtime.InteropServices;
    using System.Security.AccessControl;
    using Fsp;
    using VolumeInfo = Fsp.Interop.VolumeInfo;
    using FileInfo = Fsp.Interop.FileInfo;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    internal class WindowsFileDesc
    {
        protected const int _allocationUnit = 4096;

        protected static void ThrowIoExceptionWithHResult(int hresult)
        {
            throw new IOException(null, hresult);
        }

        protected static void ThrowIoExceptionWithWin32(int error)
        {
            ThrowIoExceptionWithHResult(unchecked((int)(0x80070000 | error)));
        }

        protected static void ThrowIoExceptionWithNtStatus(int status)
        {
            ThrowIoExceptionWithWin32((int)FileSystemBase.Win32FromNtStatus(status));
        }

        public FileStream? Stream;
        public DirectoryInfo? DirInfo;
        public DictionaryEntry[]? FileSystemInfos;
        public DriveInfo[]? Drives;

        public WindowsFileDesc(DriveInfo[] drives)
        {
            Drives = drives;
        }

        public WindowsFileDesc(FileStream stream)
        {
            Stream = stream;
        }

        public WindowsFileDesc(DirectoryInfo dirInfo)
        {
            DirInfo = dirInfo;
        }

        public static void GetFileInfoFromFileSystemInfo(
            FileSystemInfo info,
            out FileInfo fileInfo)
        {
            fileInfo.FileAttributes = (uint)info.Attributes;
            fileInfo.ReparseTag = 0;
            fileInfo.FileSize = info is System.IO.FileInfo ?
                (ulong)((System.IO.FileInfo)info).Length : 0;
            fileInfo.AllocationSize = (fileInfo.FileSize + _allocationUnit - 1)
                / _allocationUnit * _allocationUnit;
            fileInfo.CreationTime = (ulong)info.CreationTimeUtc.ToFileTimeUtc();
            fileInfo.LastAccessTime = (ulong)info.LastAccessTimeUtc.ToFileTimeUtc();
            fileInfo.LastWriteTime = (ulong)info.LastWriteTimeUtc.ToFileTimeUtc();
            fileInfo.ChangeTime = fileInfo.LastWriteTime;
            fileInfo.IndexNumber = 0;
            fileInfo.HardLinks = 0;
        }

        public int GetFileInfo(out FileInfo fileInfo)
        {
            if (Drives != null)
            {
                fileInfo = new FileInfo
                {
                    FileAttributes = (uint)FileAttributes.Directory,
                    FileSize = 0,
                    AllocationSize = 0,
                    CreationTime = (ulong)WindowsRfsHost._rootCreationTime.ToFileTime(),
                    ChangeTime = (ulong)WindowsRfsHost._rootCreationTime.ToFileTime(),
                    LastAccessTime = (ulong)WindowsRfsHost._rootCreationTime.ToFileTime(),
                    LastWriteTime = (ulong)WindowsRfsHost._rootCreationTime.ToFileTime(),
                };
            }
            else if (null != Stream)
            {
                BY_HANDLE_FILE_INFORMATION info;
                if (!GetFileInformationByHandle(Stream.SafeFileHandle.DangerousGetHandle(),
                    out info))
                    ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
                fileInfo.FileAttributes = info.dwFileAttributes;
                fileInfo.ReparseTag = 0;
                fileInfo.FileSize = (ulong)Stream.Length;
                fileInfo.AllocationSize = (fileInfo.FileSize + _allocationUnit - 1)
                    / _allocationUnit * _allocationUnit;
                fileInfo.CreationTime = info.ftCreationTime;
                fileInfo.LastAccessTime = info.ftLastAccessTime;
                fileInfo.LastWriteTime = info.ftLastWriteTime;
                fileInfo.ChangeTime = fileInfo.LastWriteTime;
                fileInfo.IndexNumber = 0;
                fileInfo.HardLinks = 0;
            }
            else
            {
                GetFileInfoFromFileSystemInfo(DirInfo!, out fileInfo);
            }
            return FileSystemBase.STATUS_SUCCESS;
        }

        public void SetBasicInfo(
            uint fileAttributes,
            ulong creationTime,
            ulong lastAccessTime,
            ulong lastWriteTime)
        {
            if (0 == fileAttributes)
                fileAttributes = (uint)System.IO.FileAttributes.Normal;
            if (null != Stream)
            {
                FILE_BASIC_INFO info = default(FILE_BASIC_INFO);
                if (unchecked((uint)(-1)) != fileAttributes)
                    info.FileAttributes = fileAttributes;
                if (0 != creationTime)
                    info.CreationTime = creationTime;
                if (0 != lastAccessTime)
                    info.LastAccessTime = lastAccessTime;
                if (0 != lastWriteTime)
                    info.LastWriteTime = lastWriteTime;
                if (!SetFileInformationByHandle(Stream.SafeFileHandle.DangerousGetHandle(),
                    0/*FileBasicInfo*/, ref info, (uint)Marshal.SizeOf(info)))
                    ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
            }
            else
            {
                if (unchecked((uint)(-1)) != fileAttributes)
                    DirInfo!.Attributes = (System.IO.FileAttributes)fileAttributes;
                if (0 != creationTime)
                    DirInfo!.CreationTimeUtc = DateTime.FromFileTimeUtc((long)creationTime);
                if (0 != lastAccessTime)
                    DirInfo!.LastAccessTimeUtc = DateTime.FromFileTimeUtc((long)lastAccessTime);
                if (0 != lastWriteTime)
                    DirInfo!.LastWriteTimeUtc = DateTime.FromFileTimeUtc((long)lastWriteTime);
            }
        }

        public uint GetFileAttributes()
        {
            FileInfo fileInfo;
            GetFileInfo(out fileInfo);
            return fileInfo.FileAttributes;
        }

        public void SetFileAttributes(uint fileAttributes)
        {
            SetBasicInfo(fileAttributes, 0, 0, 0);
        }

        public byte[] GetSecurityDescriptor()
        {
            if (null != Stream)
                return Stream.GetAccessControl().GetSecurityDescriptorBinaryForm();
            else
                return DirInfo!.GetAccessControl().GetSecurityDescriptorBinaryForm();
        }

        public void SetSecurityDescriptor(
            AccessControlSections sections,
            byte[] securityDescriptor)
        {
            int securityInformation = 0;
            if (0 != (sections & AccessControlSections.Owner))
                securityInformation |= 1/*OWNER_SECURITY_INFORMATION*/;
            if (0 != (sections & AccessControlSections.Group))
                securityInformation |= 2/*GROUP_SECURITY_INFORMATION*/;
            if (0 != (sections & AccessControlSections.Access))
                securityInformation |= 4/*DACL_SECURITY_INFORMATION*/;
            if (0 != (sections & AccessControlSections.Audit))
                securityInformation |= 8/*SACL_SECURITY_INFORMATION*/;
            if (null != Stream)
            {
                if (!SetKernelObjectSecurity(Stream.SafeFileHandle.DangerousGetHandle(),
                    securityInformation, securityDescriptor))
                    ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
            }
            else
            {
                if (!SetFileSecurityW(DirInfo!.FullName,
                    securityInformation, securityDescriptor))
                    ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
            }
        }
        public void SetDisposition(bool Safe)
        {
            if (null != Stream)
            {
                FILE_DISPOSITION_INFO info;
                info.DeleteFile = true;
                if (!SetFileInformationByHandle(Stream.SafeFileHandle.DangerousGetHandle(),
                    4/*FileDispositionInfo*/, ref info, (uint)Marshal.SizeOf(info)))
                    if (!Safe)
                        ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
            }
            else
            {
                try
                {
                    DirInfo!.Delete();
                }
                catch (Exception ex)
                {
                    if (!Safe)
                        ThrowIoExceptionWithHResult(ex.HResult);
                }
            }
        }

        public static void Rename(string fileName, string newFileName, bool replaceIfExists)
        {
            if (!MoveFileExW(fileName, newFileName, replaceIfExists ? 1U/*MOVEFILE_REPLACE_EXISTING*/ : 0))
            {
                ThrowIoExceptionWithWin32(Marshal.GetLastWin32Error());
            }
        }

        /* interop */
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public uint dwFileAttributes;
            public ulong ftCreationTime;
            public ulong ftLastAccessTime;
            public ulong ftLastWriteTime;
            public uint dwVolumeSerialNumber;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint nNumberOfLinks;
            public uint nFileIndexHigh;
            public uint nFileIndexLow;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct FILE_BASIC_INFO
        {
            public ulong CreationTime;
            public ulong LastAccessTime;
            public ulong LastWriteTime;
            public ulong ChangeTime;
            public uint FileAttributes;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct FILE_DISPOSITION_INFO
        {
            public bool DeleteFile;
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            IntPtr hFile,
            out BY_HANDLE_FILE_INFORMATION lpFileInformation);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileInformationByHandle(
            IntPtr hFile,
            int FileInformationClass,
            ref FILE_BASIC_INFO lpFileInformation,
            uint dwBufferSize);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileInformationByHandle(
            IntPtr hFile,
            int FileInformationClass,
            ref FILE_DISPOSITION_INFO lpFileInformation,
            uint dwBufferSize);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool MoveFileExW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpExistingFileName,
            [MarshalAs(UnmanagedType.LPWStr)] string lpNewFileName,
            uint dwFlags);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetFileSecurityW(
            [MarshalAs(UnmanagedType.LPWStr)] string FileName,
            int SecurityInformation,
            byte[] SecurityDescriptor);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetKernelObjectSecurity(
            IntPtr Handle,
            int SecurityInformation,
            byte[] SecurityDescriptor);
    }
}
