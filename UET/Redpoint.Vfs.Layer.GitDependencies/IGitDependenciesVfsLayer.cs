namespace Redpoint.Vfs.Layer.GitDependencies
{
    using Redpoint.Vfs.Abstractions;

    /// <summary>
    /// Additional virtual filesystem layer APIs that are specific to the GitDependencies layer.
    /// </summary>
    public interface IGitDependenciesVfsLayer : IVfsLayer
    {
        /// <summary>
        /// Initialize the GitDependencies layer. You must call this before the layer is used with a virtual filesystem driver.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An awaitable task.</returns>
        Task InitAsync(CancellationToken cancellationToken);
    }
}
