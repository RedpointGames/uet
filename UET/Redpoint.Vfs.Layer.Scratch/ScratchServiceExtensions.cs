namespace Redpoint.Vfs.Layer.Scratch
{
    using Microsoft.Extensions.DependencyInjection;

    public static class ScratchServiceExtensions
    {
        public static void AddScratchLayerFactory(this IServiceCollection services)
        {
            services.AddSingleton<IScratchVfsLayerFactory, ScratchVfsLayerFactory>();
        }
    }
}
