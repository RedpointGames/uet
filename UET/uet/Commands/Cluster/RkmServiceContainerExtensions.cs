namespace UET.Commands.Cluster
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.KubernetesManager;
    using UET.Commands.Internal.Rkm;
    using UET.Commands.Internal.RkmService;
    using UET.Services;

    internal static class RkmServiceContainerExtensions
    {
        public static void AddRkmServiceHelpers(this IServiceCollection services, bool withPathProvider, string applicationName)
        {
            services.AddKubernetesManager(withPathProvider);
            services.AddSingleton<IRkmVersionProvider, UetRkmVersionProvider>();
            services.AddSingleton<IHostApplicationLifetime>(sp => sp.GetRequiredService<RkmHostApplicationLifetime>());
            services.AddSingleton<RkmHostApplicationLifetime, RkmHostApplicationLifetime>();
            services.AddSingleton<IHostEnvironment>(sp => new RkmHostEnvironment(sp.GetRequiredService<ISelfLocation>(), applicationName));
            services.AddSingleton<IHostedServiceFromExecutable, DefaultHostedServiceFromExecutable>();
        }
    }
}
