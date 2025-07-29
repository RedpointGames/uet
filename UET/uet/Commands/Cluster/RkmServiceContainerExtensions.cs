namespace UET.Commands.Cluster
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.KubernetesManager;
    using UET.Commands.Internal.Rkm;
    using UET.Commands.Internal.RkmService;

    internal static class RkmServiceContainerExtensions
    {
        public static void AddRkmServiceHelpers(this IServiceCollection services, bool withPathProvider)
        {
            services.AddKubernetesManager(withPathProvider);
            services.AddSingleton<IRkmVersionProvider, UetRkmVersionProvider>();
            services.AddSingleton<IHostApplicationLifetime>(sp => sp.GetRequiredService<RkmHostApplicationLifetime>());
            services.AddSingleton<RkmHostApplicationLifetime, RkmHostApplicationLifetime>();
            services.AddSingleton<IHostEnvironment, RkmHostEnvironment>();
            services.AddSingleton<IHostedServiceFromExecutable, DefaultHostedServiceFromExecutable>();
        }
    }
}
