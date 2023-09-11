namespace Redpoint.Vfs.Layer.GitDependencies
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Vfs.Layer.Git;
    using Redpoint.Vfs.LocalIo;

    internal sealed class GitDependenciesVfsLayerFactory : IGitDependenciesVfsLayerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public GitDependenciesVfsLayerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IGitDependenciesVfsLayer CreateLayer(
            string cachePath,
            IGitVfsLayer nextLayer)
        {
            return new GitDependenciesVfsLayer(
                _serviceProvider.GetRequiredService<ILogger<GitDependenciesVfsLayer>>(),
                _serviceProvider.GetRequiredService<ILocalIoVfsFileFactory>(),
                cachePath,
                nextLayer);
        }
    }
}
