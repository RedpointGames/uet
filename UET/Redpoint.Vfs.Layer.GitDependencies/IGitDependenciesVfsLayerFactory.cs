namespace Redpoint.Vfs.Layer.GitDependencies
{
    using Redpoint.Vfs.Layer.Git;

    /// <summary>
    /// A factory which creates GitDependencies virtual filesystem layer instances.
    /// </summary>
    public interface IGitDependenciesVfsLayerFactory
    {
        /// <summary>
        /// Creates a GitDependencies virtual filesystem layer which is layered on top of the specified <see cref="IGitVfsLayer"/>.
        /// </summary>
        /// <param name="cachePath">The path where downloaded GitDependencies are stored after first access.</param>
        /// <param name="nextLayer">The parent Git virtual filesystem layer to serve from.</param>
        /// <returns>The new GitDependencies virtual filesystem layer.</returns>
        IGitDependenciesVfsLayer CreateLayer(
            string cachePath,
            IGitVfsLayer nextLayer);
    }
}
