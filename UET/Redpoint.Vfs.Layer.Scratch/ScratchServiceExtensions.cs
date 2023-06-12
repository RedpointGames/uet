namespace Redpoint.Vfs.Layer.Scratch
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Registers the <see cref="IScratchVfsLayerFactory"/> implementation with dependency injection.
    /// </summary>
    public static class ScratchServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="IScratchVfsLayerFactory"/> implementation with dependency injection.
        /// </summary>
        public static void AddScratchLayerFactory(this IServiceCollection services)
        {
            services.AddSingleton<IScratchVfsLayerFactory, ScratchVfsLayerFactory>();
        }
    }
}
