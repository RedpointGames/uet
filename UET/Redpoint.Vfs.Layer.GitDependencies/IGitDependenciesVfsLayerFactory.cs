namespace Redpoint.Vfs.Layer.GitDependencies
{
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.Layer.Git;

    public interface IGitDependenciesVfsLayerFactory
    {
        IGitDependenciesVfsLayer CreateLayer(
            string cachePath,
            IGitVfsLayer nextLayer);
    }
}
