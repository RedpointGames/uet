namespace Redpoint.Vfs.Layer.Folder
{
    using Redpoint.Vfs.Abstractions;

    public interface IFolderVfsLayerFactory
    {
        IVfsLayer CreateLayer(
            string path,
            IVfsLayer? nextLayer);
    }
}