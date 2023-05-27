namespace Redpoint.Vfs.Driver
{
    using Redpoint.Vfs.Abstractions;

    public interface IVfsDriverFactory
    {
        IVfsDriver? InitializeAndMount(
            IVfsLayer projectionLayer,
            string mountPath,
            VfsDriverOptions? options);
    }
}
