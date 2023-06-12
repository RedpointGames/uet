namespace Redpoint.Vfs.Abstractions
{
    using System.Runtime.Versioning;

    /// <summary>
    /// This interface should be implemented by virtual filesystem drivers so that virtual filesystem files can use asynchronous I/O.
    /// </summary>
    [SupportedOSPlatform("windows6.2")]
    public interface IAsyncIoProcessing
    {
        /// <summary>
        /// Allocated a NATIVEOVERLAPPED structure such that <paramref name="callback"/> is called when the asynchronous I/O operation completes.
        /// </summary>
        /// <param name="ioHandle">The handle capable of asynchronous I/O.</param>
        /// <param name="requestHint">The unique request hint such that the request can be mapped back to the correct <paramref name="callback"/> when the native API call returns.</param>
        /// <param name="callback">The callback to fire when the API callback completes.</param>
        /// <returns>The newly allocated NATIVEOVERLAPPED structure.</returns>
        unsafe AsyncIoNativeOverlapped* AllocateNativeOverlapped(IAsyncIoHandle ioHandle, ulong requestHint, VfsFileAsyncCallback callback);

        /// <summary>
        /// Called to cancel and deallocate the NATIVEOVERLAPPED structure when the API call is no longer expected to proceed.
        /// </summary>
        /// <param name="overlapped">The pointer of the <see cref="AsyncIoNativeOverlapped"/> structure.</param>
        unsafe void CancelNativeOverlapped(nint overlapped);
    }
}
