namespace Redpoint.Vfs.Layer.GitDependencies
{
    using Redpoint.Vfs.Layer.Git;

    public interface IGitDependenciesVfsLayerFactory
    {
        IGitDependenciesVfsLayer CreateLayer(
            string cachePath,
            IGitVfsLayer nextLayer);
    }
}
