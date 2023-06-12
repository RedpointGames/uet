namespace Redpoint.Vfs.Layer.Folder
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Registers the <see cref="IFolderVfsLayerFactory"/> implementation with dependency injection.
    /// </summary>
    public static class FolderServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="IFolderVfsLayerFactory"/> implementation with dependency injection.
        /// </summary>
        public static void AddFolderLayerFactory(this IServiceCollection services)
        {
            services.AddSingleton<IFolderVfsLayerFactory, FolderVfsLayerFactory>();
        }
    }
}
