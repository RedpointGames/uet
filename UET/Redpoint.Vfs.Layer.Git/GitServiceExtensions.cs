namespace Redpoint.Vfs.Layer.Git
{
    using Microsoft.Extensions.DependencyInjection;

    public static class GitServiceExtensions
    {
        public static void AddGitLayerFactory(this IServiceCollection services)
        {
            services.AddSingleton<IGitVfsLayerFactory, GitVfsLayerFactory>();
        }
    }
}
