namespace Redpoint.Vfs.Layer.Folder
{
    using Microsoft.Extensions.DependencyInjection;

    public static class FolderServiceExtensions
    {
        public static void AddFolderLayerFactory(this IServiceCollection services)
        {
            services.AddSingleton<IFolderVfsLayerFactory, FolderVfsLayerFactory>();
        }
    }
}
