namespace Redpoint.CredentialDiscovery
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extensions for registering credential discovery services.
    /// </summary>
    public static class CredentialDiscoveryServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="ICredentialDiscovery"/> service.
        /// </summary>
        /// <param name="services">The service collection to register services with.</param>
        public static void AddCredentialDiscovery(this IServiceCollection services)
        {
            services.AddSingleton<ICredentialDiscovery, DefaultCredentialDiscovery>();
        }
    }
}
