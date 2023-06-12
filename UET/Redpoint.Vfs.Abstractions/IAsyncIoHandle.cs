namespace Redpoint.Vfs.Abstractions
{
    using Microsoft.Win32.SafeHandles;
    using System.Runtime.Versioning;

    /// <summary>
    /// Represents that an <see cref="IVfsFileHandle{T}"/> is capable of asynchronous I/O.
    /// </summary>
    [SupportedOSPlatform("windows6.2")]
    public interface IAsyncIoHandle
    {
        /// <summary>
        /// The Windows file handle for use with asynchronous I/O.
        /// </summary>
        SafeFileHandle SafeFileHandle { get; }
    }
}
