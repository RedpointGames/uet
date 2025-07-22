namespace Redpoint.CloudFramework
{
    using Google.Cloud.Datastore.V1;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CloudFramework.Counter;
    using Redpoint.CloudFramework.Locking;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Repository.Contention;
    using Redpoint.CloudFramework.Repository.Converters.Expression;
    using Redpoint.CloudFramework.Repository.Converters.Model;
    using Redpoint.CloudFramework.Repository.Datastore;
    using Redpoint.CloudFramework.Repository.Hooks;
    using Redpoint.CloudFramework.Repository.Layers;
    using Redpoint.CloudFramework.Repository.Migration;
    using Redpoint.CloudFramework.Repository;
    using System.Diagnostics.CodeAnalysis;
    using Redpoint.CloudFramework.Infrastructure;
    using Redpoint.CloudFramework.Repository.Converters.Timestamp;
    using Redpoint.CloudFramework.Repository.Converters.Value;
    using Redpoint.CloudFramework.Event;
    using Redpoint.CloudFramework.Metric;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Options;
    using Quartz;
    using Redpoint.CloudFramework.Processor;
    using Redpoint.CloudFramework.GoogleInfrastructure;
    using Redpoint.CloudFramework.Repository.Validation;

    /// <summary>
    /// Provides additional service registration methods for <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the specified prefix provider to the service collection.
        /// </summary>
        /// <typeparam name="T">The prefix provider implementation.</typeparam>
        /// <param name="services">The service collection to register it with.</param>
        public static void AddPrefixProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services) where T : class, IPrefixProvider
        {
            services.AddSingleton<IPrefixProvider, T>();
        }

        /// <summary>
        /// Adds core registrations for Cloud Framework repository and related services. You don't need to call this if you're using <see cref="CloudFramework"/> to define your application.
        /// </summary>
        /// <param name="services">The service collection to register services with.</param>
        public static void AddCloudFrameworkCore(this IServiceCollection services)
        {
            services.AddSingleton<IMetricService, DiagnosticSourceMetricService>();

            services.AddSingleton<IEventApi, EventApi>();

            services.AddSingleton<IGlobalPrefix, GlobalPrefix>();
            services.AddSingleton<IRandomStringGenerator, RandomStringGenerator>();
            services.AddSingleton<IInstantTimestampConverter, DefaultInstantTimestampConverter>();
            services.AddSingleton<IInstantTimestampJsonConverter, DefaultInstantTimestampJsonConverter>();

            services.AddSingleton<IValueConverter, BooleanValueConverter>();
            services.AddSingleton<IValueConverter, DoubleValueConverter>();
            services.AddSingleton<IValueConverter, EmbeddedEntityValueConverter>();
            services.AddSingleton<IValueConverter, GeopointValueConverter>();
            services.AddSingleton<IValueConverter, GlobalKeyArrayValueConverter>();
            services.AddSingleton<IValueConverter, GlobalKeyValueConverter>();
            services.AddSingleton<IValueConverter, IntegerValueConverter>();
            services.AddSingleton<IValueConverter, UnsignedIntegerValueConverter>();
            services.AddSingleton<IValueConverter, UnsignedIntegerArrayValueConverter>();
            services.AddSingleton<IValueConverter, JsonValueConverter>();
            services.AddSingleton<IValueConverter, KeyArrayValueConverter>();
            services.AddSingleton<IValueConverter, KeyValueConverter>();
            services.AddSingleton<IValueConverter, LocalKeyValueConverter>();
            services.AddSingleton<IValueConverter, StringArrayValueConverter>();
            services.AddSingleton<IValueConverter, StringEnumSetValueConverter>();
            services.AddSingleton<IValueConverter, StringEnumArrayValueConverter>();
            services.AddSingleton<IValueConverter, StringEnumValueConverter>();
            services.AddSingleton<IValueConverter, StringValueConverter>();
            services.AddSingleton<IValueConverter, TimestampValueConverter>();
            services.AddSingleton<IValueConverter, UnsafeKeyValueConverter>();
            services.AddSingleton<IValueConverterProvider, DefaultValueConverterProvider>();
        }

        /// <summary>
        /// Adds the Google Cloud services to the service collection. You don't need to call this if you're using <see cref="CloudFramework"/> to define your application.
        /// </summary>
        /// <param name="services">The service collection to register services with.</param>
        public static void AddCloudFrameworkGoogleCloud(this IServiceCollection services)
        {
            services.AddSingleton<IGoogleServices, GoogleServices>();
            services.AddSingleton<IGoogleApiRetry, GoogleApiRetry>();
        }

        /// <summary>
        /// Adds the Cloud Framework repository and Datastore related services. You don't need to call this if you're using <see cref="CloudFramework"/> to define your application.
        /// </summary>
        /// <param name="services">The service collection to register services with.</param>
        /// <param name="enableMigrations">If true, database migrations will be applied on startup.</param>
        public static void AddCloudFrameworkRepository(
            this IServiceCollection services,
            bool enableMigrations,
            bool enableRedis)
        {
            services.AddSingleton<IModelValidator, DefaultModelValidator>();
            services.AddSingleton<IGlobalRepository, DatastoreGlobalRepository>();
            services.AddSingleton<IGlobalRepositoryHook[]>(svc => svc.GetServices<IGlobalRepositoryHook>().ToArray());
            services.AddSingleton<IModelConverter<string>, JsonModelConverter>();
            services.AddSingleton<IModelConverter<Entity>, EntityModelConverter>();
            services.AddSingleton<IExpressionConverter, DefaultExpressionConverter>();
            if (enableRedis)
            {
                services.AddSingleton<IRedisCacheRepositoryLayer, RedisCacheRepositoryLayer>();
            }
            services.AddSingleton<IDatastoreRepositoryLayer, DatastoreRepositoryLayer>();
            services.AddSingleton<IGlobalLockService, DatastoreBasedGlobalLockService>();
            if (enableMigrations)
            {
                services.AddHostedService<DatastoreStartupMigrator>();
                services.AddTransient<RegisteredModelMigratorBase[]>(sp => sp.GetServices<RegisteredModelMigratorBase>().ToArray());
            }
            services.AddSingleton<IGlobalShardedCounter, DefaultGlobalShardedCounter>();
            services.AddSingleton<IDatastoreContentionRetry, DefaultDatastoreContentionRetry>();
        }

        /// <summary>
        /// Adds Quartz background processing services for Cloud Framework. You don't need to call this if you're using <see cref="CloudFramework"/> to define your application.
        /// </summary>
        /// <param name="services">The service collection to register services with.</param>
        public static void AddCloudFrameworkQuartz(this IServiceCollection services)
        {
            services.TryAddEnumerable(new[]
            {
                ServiceDescriptor.Singleton<IPostConfigureOptions<QuartzOptions>, QuartzCloudFrameworkPostConfigureOptions>()
            });
            services.AddQuartz(options =>
            {
                options.UseSimpleTypeLoader();
                // @todo: In future we should support clustering, but for now we do not.
                options.UseInMemoryStore();
            });
            services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        }
    }
}
