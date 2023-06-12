namespace Redpoint.Vfs.Abstractions
{
    /// <summary>
    /// Represents an open handle to a virtual filesystem file.
    /// </summary>
    /// <typeparam name="T">The <see cref="IVfsFile"/> implementation or interface.</typeparam>
    public interface IVfsFileHandle<out T> : IDisposable where T : IVfsFile
    {
        /// <summary>
        /// The virtual filesystem file.
        /// </summary>
        T VfsFile { get; }
    }
}
