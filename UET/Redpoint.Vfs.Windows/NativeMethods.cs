using System.Runtime.CompilerServices;

[assembly: DisableRuntimeMarshalling]

namespace Redpoint.Vfs.Windows
{
    using Microsoft.Win32.SafeHandles;
    using Redpoint.Vfs.Abstractions;
    using System.Runtime.InteropServices;

    public partial class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public ref struct NativeFileEndOfFileInfo
        {
            public long EndOfFile;
        }

        public const int FileInformationClass_FileEndOfFileInfo = 0x6; // _FILE_INFO_BY_HANDLE_CLASS.FileEndOfFileInfo

        // No operation should use SetFilePointerEx as it is not thread safe. All operations must use lpOverlapped!

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ReadFile(SafeFileHandle hFile, nint lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, nint lpOverlapped);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool WriteFile(SafeFileHandle hFile, nint lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, nint lpOverlapped);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool FlushFileBuffers(SafeFileHandle hFile);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetFileSizeEx(SafeFileHandle hFile, out long lpFileSizeHigh);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetFileInformationByHandle(SafeFileHandle hFile, int FileInformationClass, in NativeFileEndOfFileInfo lpFileInformation, int dwBufferSize);

        [LibraryImport("Kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeviceIoControl(
            SafeFileHandle hDevice,
            int dwIoControlCode,
            nint InBuffer,
            int nInBufferSize,
            nint OutBuffer,
            int nOutBufferSize,
            ref int pBytesReturned,
            in AsyncIoNativeOverlapped lpOverlapped
        );

        [LibraryImport("Kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetOverlappedResult(
            SafeFileHandle hFile,
            nint lpOverlapped,
            out uint lpNumberOfBytesTransferred,
            [MarshalAs(UnmanagedType.Bool)] bool wait);
    }
}
