namespace Redpoint.KubernetesManager.Tpm
{
    using Microsoft.Extensions.DependencyInjection;

    public static class KubernetesManagerTpmServiceExtensions
    {
        public static void AddRkmTpm(this IServiceCollection services)
        {
            services.AddSingleton<ITpmService, DefaultTpmService>();
            services.AddSingleton<ITpmCertificateService, DefaultTpmCertificateService>();
        }
    }
}
