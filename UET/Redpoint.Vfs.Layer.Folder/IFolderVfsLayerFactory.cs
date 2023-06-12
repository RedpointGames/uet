namespace Redpoint.Vfs.Layer.Folder
{
    using Redpoint.Vfs.Abstractions;

    /// <summary>
    /// A factory which creates folder virtual filesystem layer instances.
    /// </summary>
    public interface IFolderVfsLayerFactory
    {
        /// <summary>
        /// Creates a folder virtual filesystem layer, which combines the parent layer with the content at the specified path.
        /// </summary>
        /// <param name="path">The path on the local system.</param>
        /// <param name="nextLayer">The parent layer to combine the content with.</param>
        /// <returns>The new folder virtual filesystem layer.</returns>
        IVfsLayer CreateLayer(
            string path,
            IVfsLayer? nextLayer);
    }
}