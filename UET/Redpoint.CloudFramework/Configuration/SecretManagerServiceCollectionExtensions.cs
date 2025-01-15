namespace Redpoint.CloudFramework.Configuration
{
    using Google.Cloud.SecretManager.V1;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Method which can be used to register the <see cref="IAutoRefreshingSecretFactory"/> service
    /// with a service collection.
    /// </summary>
    public static class SecretManagerServiceCollectionExtensions
    {
        private static ISecretManagerNotificationManager? _superSingletonNotificationManager = null;

        private static void AddSecretManagerBase(this IServiceCollection services, bool isolatedNotificationManager)
        {
            services.AddSingleton<ISecretManagerAccess, DefaultSecretManagerAccess>();
            if (isolatedNotificationManager)
            {
                services.AddSingleton<ISecretManagerNotificationManager, DefaultSecretManagerNotificationManager>();
            }
            else
            {
                services.AddSingleton<DefaultSecretManagerNotificationManager>();
                services.AddSingleton(sp =>
                {
                    // @note: The notification manager is registered as a "super singleton", which is always
                    // the same instance in the application, even across different service providers and ensures
                    // that the hosted service that runs at runtime in the ASP.NET uses the same instance that
                    // the configuration system uses.
                    if (_superSingletonNotificationManager == null)
                    {
                        _superSingletonNotificationManager = sp.GetRequiredService<DefaultSecretManagerNotificationManager>();
                    }
                    return _superSingletonNotificationManager;
                });
            }
            services.AddSingleton<IAutoRefreshingSecretFactory, DefaultAutoRefreshingSecretFactory>();
        }

        /// <summary>
        /// Registers a hosted service which will clean up Google Cloud Secret Manager notification
        /// subscriptions when the application exits.
        /// </summary>
        /// <param name="services">The service collection to register services into.</param>
        public static void AddSecretManagerRuntime(this IServiceCollection services)
        {
            services.AddSecretManagerBase(false);

            services.AddHostedService<SecretManagerSubscriptionCleanupHostedService>();
        }

        /// <summary>
        /// Registers <see cref="IAutoRefreshingSecretFactory"/> and <see cref="IConfigurationSource"/>
        /// services into the service collection.
        /// </summary>
        /// <param name="services">The service collection to register services into.</param>
        /// <param name="requireSuccessfulLoad">If true, the <see cref="IConfigurationSource"/> will throw if the secret fails to load.</param>
        /// <param name="secretName">The secret that the <see cref="IConfigurationSource"/> should use as a backing store. Defaults to "appsettings".</param>
        /// <param name="isolatedNotificationManager">For automation testing only.</param>
        public static void AddSecretManagerConfiguration(
            this IServiceCollection services,
            bool requireSuccessfulLoad,
            string secretName = "appsettings",
            bool isolatedNotificationManager = false)
        {
            services.AddSecretManagerBase(isolatedNotificationManager);

            services.AddSingleton<ISecretManagerConfigurationSourceBehaviour>(new DefaultSecretManagerConfigurationSourceBehaviour(secretName, requireSuccessfulLoad));
            services.AddSingleton<IConfigurationSource, SecretManagerConfigurationSource>();
            services.AddTransient<SecretManagerConfigurationProvider>();
        }
    }
}
