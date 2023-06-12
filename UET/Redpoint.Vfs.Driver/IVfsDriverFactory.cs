namespace Redpoint.Vfs.Driver
{
    using Redpoint.Vfs.Abstractions;

    /// <summary>
    /// Represents a factory that can create virtual filesystem drivers.
    /// </summary>
    public interface IVfsDriverFactory
    {
        /// <summary>
        /// Initializes a virtual filesystem driver that serves contents from the specified <paramref name="layer"/>
        /// </summary>
        /// <param name="layer">The layer to serve content from.</param>
        /// <param name="mountPath">The path to serve the virtual filesystem at.</param>
        /// <param name="options">Optional additional settings for the virtual filesystem driver.</param>
        /// <returns>The new virtual filesystem driver, or null if the virtual filesystem driver could not be created.</returns>
        IVfsDriver? InitializeAndMount(
            IVfsLayer layer,
            string mountPath,
            VfsDriverOptions? options);
    }
}
