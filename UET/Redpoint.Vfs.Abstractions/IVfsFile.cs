namespace Redpoint.Vfs.Abstractions
{
    /// <summary>
    /// The callback for asynchronous I/O operations.
    /// </summary>
    /// <param name="requestHint">The original request hint that was provided to <see cref="IAsyncIoProcessing.AllocateNativeOverlapped(IAsyncIoHandle, ulong, VfsFileAsyncCallback)"/>.</param>
    /// <param name="status">The Win32 result.</param>
    /// <param name="bytesTransferred">The bytes transferred as part of the I/O operation.</param>
    public delegate void VfsFileAsyncCallback(ulong requestHint, int status, uint bytesTransferred);

    /// <summary>
    /// Represents a virtual filesystem file.
    /// </summary>
    public interface IVfsFile
    {
        /// <summary>
        /// Synchronously read from a virtual filesystem file into a managed buffer.
        /// </summary>
        /// <param name="buffer">The buffer to read bytes into.</param>
        /// <param name="bytesRead">The number of bytes that were read.</param>
        /// <param name="offset">The offset to read at.</param>
        /// <returns>The Win32 result.</returns>
        int ReadFile(byte[] buffer, out uint bytesRead, long offset);

        /// <summary>
        /// Synchronously read from a virtual filesystem file into unmanaged memory.
        /// </summary>
        /// <param name="buffer">The memory pointer to read into.</param>
        /// <param name="bufferLength">The length of the buffer, and the number of bytes to read from the file.</param>
        /// <param name="bytesRead">The number of bytes that were actually read.</param>
        /// <param name="offset">The offset to read at.</param>
        /// <returns>The Win32 result.</returns>
        int ReadFileUnsafe(IntPtr buffer, uint bufferLength, out uint bytesRead, long offset);

        /// <summary>
        /// Asynchronously read from a virtual filesystem file into unmanaged memory.
        /// </summary>
        /// <param name="buffer">The memory pointer to read into.</param>
        /// <param name="bufferLength">The length of the buffer, and the number of bytes to read from the file.</param>
        /// <param name="bytesReadOnSyncResult">If the operation completes synchronously, this is the number of bytes read.</param>
        /// <param name="offset">The offset to read at.</param>
        /// <param name="requestHint">The unique request hint such that <paramref name="callback"/> can be mapped correctly.</param>
        /// <param name="asyncIo">The interface for allocating NATIVEOVERLAPPED.</param>
        /// <param name="callback">The callback to fire when the asynchronous I/O completes.</param>
        /// <returns>The Win32 result. This will be IoPending if the read is completing asynchronously.</returns>
        int ReadFileUnsafeAsync(IntPtr buffer, uint bufferLength, out uint bytesReadOnSyncResult, long offset, ulong requestHint, IAsyncIoProcessing asyncIo, VfsFileAsyncCallback callback);

        /// <summary>
        /// Synchronously write to a virtual filesystem from a managed buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write bytes from.</param>
        /// <param name="bytesToWrite">The number of bytes to write.</param>
        /// <param name="bytesWritten">The number of bytes that were written.</param>
        /// <param name="offset">The offset to write at.</param>
        /// <returns>The Win32 result.</returns>
        int WriteFile(byte[] buffer, uint bytesToWrite, out uint bytesWritten, long offset);

        /// <summary>
        /// Synchronously write to a virtual filesystem from unmanaged memory.
        /// </summary>
        /// <param name="buffer">The memory pointer to write from.</param>
        /// <param name="bufferLength">The number of bytes to write and at most, the length of the unmanaged buffer.</param>
        /// <param name="bytesWritten">The number of bytes that were written.</param>
        /// <param name="offset">The offset to write at.</param>
        /// <returns>The Win32 result.</returns>
        int WriteFileUnsafe(IntPtr buffer, uint bufferLength, out uint bytesWritten, long offset);

        /// <summary>
        /// Asynchronously write to a virtual filesystem from unmanaged memory.
        /// </summary>
        /// <param name="buffer">The memory pointer to write from.</param>
        /// <param name="bufferLength">The number of bytes to write and at most, the length of the unmanaged buffer.</param>
        /// <param name="bytesWrittenOnSyncResult">If the operation completes synchronously, this is the number of bytes written.</param>
        /// <param name="offset">The offset to write at.</param>
        /// <param name="requestHint">The unique request hint such that <paramref name="callback"/> can be mapped correctly.</param>
        /// <param name="asyncIo">The interface for allocating NATIVEOVERLAPPED.</param>
        /// <param name="callback">The callback to fire when the asynchronous I/O completes.</param>
        /// <returns>The Win32 result. This will be IoPending if the read is completing asynchronously.</returns>
        int WriteFileUnsafeAsync(IntPtr buffer, uint bufferLength, out uint bytesWrittenOnSyncResult, long offset, ulong requestHint, IAsyncIoProcessing asyncIo, VfsFileAsyncCallback callback);

        /// <summary>
        /// Sets the end of the virtual filesystem file to <paramref name="length"/>.
        /// </summary>
        /// <param name="length">The new length of the virtual filesystem file.</param>
        /// <returns>The Win32 result.</returns>
        int SetEndOfFile(long length);

        /// <summary>
        /// The current length of the virtual filesystem file.
        /// </summary>
        long Length { get; }
    }
}
