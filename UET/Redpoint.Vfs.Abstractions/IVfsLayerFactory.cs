namespace Redpoint.Vfs.Driver
{
    using Redpoint.Vfs.Abstractions;
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Represents a factory that can convert a generic URL into a concrete <see cref="IVfsLayer"/> implementation.
    /// </summary>
    public interface IVfsLayerFactory
    {
        /// <summary>
        /// Constructs a VFS layer based on the specified URL.
        /// </summary>
        /// <param name="sourceUri">The URL to construct a VFS layer from.</param>
        /// <returns>The new VFS layer, or null if this factory doesn't handle the specified URL.</returns>
        IVfsLayer? ConstructLayerFromUrl(Uri sourceUri);
    }
}
