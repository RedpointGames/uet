namespace Redpoint.KubernetesManager.Manifest
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.KubernetesManager.Manifest.Client;

    public static class RkmManifestServiceCollectionExtensions
    {
        public static void AddRkmManifest(this IServiceCollection services)
        {
            services.AddSingleton<IGenericManifestClient, DefaultGenericManifestClient>();
        }
    }
}
