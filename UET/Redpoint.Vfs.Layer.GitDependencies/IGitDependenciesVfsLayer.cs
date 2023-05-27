namespace Redpoint.Vfs.Layer.GitDependencies
{
    using Redpoint.Vfs.Abstractions;

    public interface IGitDependenciesVfsLayer : IVfsLayer
    {
        Task InitAsync(CancellationToken cancellationToken);
    }
}
