namespace Redpoint.Vfs.Layer.GitDependencies
{
    using Microsoft.Extensions.DependencyInjection;

    public static class GitDependenciesServiceExtensions
    {
        public static void AddGitDependenciesLayerFactory(this IServiceCollection services)
        {
            services.AddSingleton<IGitDependenciesVfsLayerFactory, GitDependenciesVfsLayerFactory>();
        }
    }
}
