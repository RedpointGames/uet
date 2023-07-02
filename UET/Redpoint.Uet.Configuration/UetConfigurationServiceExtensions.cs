namespace Redpoint.Uet.Configuration
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.Configuration.Dynamic;

    public static class UetConfigurationServiceExtensions
    {
        public static void AddDynamicProvider<TDistribution, TConfigBase, TImplementation>(this IServiceCollection services) where TImplementation : class, IDynamicProvider<TDistribution, TConfigBase>
        {
            services.AddSingleton<TImplementation, TImplementation>();
            services.AddSingleton<IDynamicProvider<TDistribution, TConfigBase>, TImplementation>(sp => sp.GetRequiredService<TImplementation>());
            services.AddSingleton<IDynamicProviderRegistration, TImplementation>(sp => sp.GetRequiredService<TImplementation>());
            if (typeof(TImplementation).IsAssignableTo(typeof(IDynamicReentrantExecutor<TDistribution>)))
            {
                services.AddSingleton(sp => (IDynamicReentrantExecutor<TDistribution>)sp.GetRequiredService<TImplementation>());
            }
        }
    }
}
