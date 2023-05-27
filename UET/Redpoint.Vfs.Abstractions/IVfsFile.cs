namespace Redpoint.Vfs.Abstractions
{
    public delegate void VfsFileAsyncCallback(ulong requestHint, int status, uint bytesTransferred);

    public interface IVfsFile
    {
        int ReadFile(byte[] buffer, out uint bytesRead, long offset);

        int ReadFileUnsafe(IntPtr buffer, uint bufferLength, out uint bytesRead, long offset);

        int ReadFileUnsafeAsync(IntPtr buffer, uint bufferLength, out uint bytesReadOnSyncResult, long offset, ulong requestHint, IAsyncIoProcessing asyncIo, VfsFileAsyncCallback callback);

        int WriteFile(byte[] buffer, uint bytesToWrite, out uint bytesWritten, long offset);

        int WriteFileUnsafe(IntPtr buffer, uint bufferLength, out uint bytesWritten, long offset);

        int WriteFileUnsafeAsync(IntPtr buffer, uint bufferLength, out uint bytesWrittenOnSyncResult, long offset, ulong requestHint, IAsyncIoProcessing asyncIo, VfsFileAsyncCallback callback);

        int SetEndOfFile(long length);

        long Length { get; }
    }
}
