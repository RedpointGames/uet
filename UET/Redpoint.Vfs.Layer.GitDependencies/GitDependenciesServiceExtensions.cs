namespace Redpoint.Vfs.Layer.GitDependencies
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Registers the <see cref="IGitDependenciesVfsLayerFactory"/> implementation with dependency injection.
    /// </summary>
    public static class GitDependenciesServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="IGitDependenciesVfsLayerFactory"/> implementation with dependency injection.
        /// </summary>
        public static void AddGitDependenciesLayerFactory(this IServiceCollection services)
        {
            services.AddSingleton<IGitDependenciesVfsLayerFactory, GitDependenciesVfsLayerFactory>();
        }
    }
}
