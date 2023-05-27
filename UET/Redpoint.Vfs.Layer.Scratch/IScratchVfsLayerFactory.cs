namespace Redpoint.Vfs.Layer.Scratch
{
    using Redpoint.Vfs.Abstractions;

    public interface IScratchVfsLayerFactory
    {
        IScratchVfsLayer CreateLayer(
            string path,
            IVfsLayer? nextLayer,
            bool enableCorrectnessChecks = false);
    }
}
