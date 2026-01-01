namespace Redpoint.Tpm
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Tpm.Internal;

    /// <summary>
    /// Extension methods to register TPM services into a service collection.
    /// </summary>
    public static class TpmServiceCollectionExtensions
    {
        /// <summary>
        /// Register the <see cref="ITpmSecuredHttp"/> service with the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public static void AddTpm(this IServiceCollection services)
        {
            services.AddSingleton<ITpmService, DefaultTpmService>();
            services.AddSingleton<ITpmSecuredHttp, DefaultTpmSecuredHttp>();
        }
    }
}
