namespace Redpoint.Vfs.Client
{
    /// <summary>
    /// A programmatic interface for mounting and unmounting VFS layers.
    /// </summary>
    public interface IVfsClient
    {
        /// <summary>
        /// Attempt to mount the specified source URI at the specified path.
        /// </summary>
        /// <param name="vfsHostName">The name of the VFS host service that is installed on the local machine. On Windows, this is used to connect to the named pipe for mount operations. On macOS, this is used as the 'type' argument to the mount command.</param>
        /// <param name="sourceUri">The source URI for the VFS. The VFS layer will be constructed based on this URI in the VFS driver host.</param>
        /// <param name="mountPath">The path to mount the VFS to.</param>
        /// <exception cref="MountFailureException">Thrown if the mount fails.</exception>
        /// <returns>The task that can be awaited.</returns>
        Task MountAsync(
            string vfsHostName,
            Uri sourceUri,
            string mountPath);

        /// <summary>
        /// Attempt to unmount the VFS at the previous mount path.
        /// </summary>
        /// <param name="vfsHostName">The name of the VFS host service that is installed on the local machine. On Windows, this is used to connect to the named pipe for mount operations. On macOS, this is unused since we just unmount based on the path.</param>
        /// <param name="mountPath">The path that the VFS was mounted at.</param>
        /// <returns>The task that can be awaited.</returns>
        Task UnmountAsync(
            string vfsHostName,
            string mountPath);
    }
}
