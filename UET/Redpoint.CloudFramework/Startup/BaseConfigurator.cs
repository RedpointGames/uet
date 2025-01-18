extern alias RDCommandLine;

namespace Redpoint.CloudFramework.Startup
{
    using Google.Api;
    using Google.Cloud.Datastore.V1;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using OpenTelemetry.Metrics;
    using Quartz;
    using RDCommandLine::Microsoft.Extensions.Logging.Console;
    using Redpoint.CloudFramework.BigQuery;
    using Redpoint.CloudFramework.Configuration;
    using Redpoint.CloudFramework.Counter;
    using Redpoint.CloudFramework.Event;
    using Redpoint.CloudFramework.Event.PubSub;
    using Redpoint.CloudFramework.GoogleInfrastructure;
    using Redpoint.CloudFramework.Infrastructure;
    using Redpoint.CloudFramework.Locking;
    using Redpoint.CloudFramework.Metric;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Processor;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Contention;
    using Redpoint.CloudFramework.Repository.Converters.Expression;
    using Redpoint.CloudFramework.Repository.Converters.Model;
    using Redpoint.CloudFramework.Repository.Converters.Timestamp;
    using Redpoint.CloudFramework.Repository.Converters.Value;
    using Redpoint.CloudFramework.Repository.Datastore;
    using Redpoint.CloudFramework.Repository.Hooks;
    using Redpoint.CloudFramework.Repository.Layers;
    using Redpoint.CloudFramework.Repository.Migration;
    using Redpoint.CloudFramework.Storage;
    using Redpoint.CloudFramework.Tracing;
    using Redpoint.Logging.SingleLine;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;

    internal class BaseConfigurator<TBase> : IBaseConfigurator<TBase>
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        internal Type? _prefixProvider = null;
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        internal Type _currentTenantService = typeof(SingleCurrentTenantService);
        internal GoogleCloudUsageFlag _googleCloudUsage = GoogleCloudUsageFlag.Default;
        internal bool _requireGoogleCloudSecretManagerLoad = false;
        internal bool _isInteractiveCLIApp = false;
        internal Action<IHostEnvironment, IConfigurationBuilder>? _customConfigLayers = null;

        public TBase UsePrefixProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : IPrefixProvider
        {
            _prefixProvider = typeof(T);
            return (TBase)(object)this;
        }

        public TBase UseMultiTenant<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : ICurrentTenantService
        {
            _currentTenantService = typeof(T);
            return (TBase)(object)this;
        }

        public TBase UseGoogleCloud(GoogleCloudUsageFlag usageFlag)
        {
            _googleCloudUsage = usageFlag;
            return (TBase)(object)this;
        }

        public TBase UseCustomConfigLayers(Action<IHostEnvironment, IConfigurationBuilder> customConfigLayers)
        {
            _customConfigLayers = customConfigLayers;
            return (TBase)(object)this;
        }

        public TBase RequireGoogleCloudSecretManagerConfiguration()
        {
            _requireGoogleCloudSecretManagerLoad = true;
            return (TBase)(object)this;
        }

        protected void ValidateConfiguration()
        {
            if (_requireGoogleCloudSecretManagerLoad)
            {
                // Use of RequireGoogleCloudSecretManagerConfiguration implies Secret Manager service.
                _googleCloudUsage |= GoogleCloudUsageFlag.SecretManager;
            }
        }

        protected virtual void ConfigureAppConfiguration(IHostEnvironment env, IConfigurationBuilder config)
        {
            config.Sources.Clear();

            if (_isInteractiveCLIApp)
            {
                config.AddJsonFile($"appsettings.CLI.json", optional: false, reloadOnChange: true);
            }
            else
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: !env.IsProduction());
                if (env.IsDevelopment() || env.IsStaging())
                {
                    config.AddJsonFile("appsettings.DevelopmentStaging.json", optional: true, reloadOnChange: true);
                }
                config.AddJsonFile($"appsettings.{env.EnvironmentName}.json",
                                     optional: true, reloadOnChange: !env.IsProduction());
            }

            var configPath = Environment.GetEnvironmentVariable("CLOUD_FRAMEWORK_CONFIG_PATH");
            if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            {
                config.AddJsonFile(configPath, optional: false, reloadOnChange: false);
            }

            if (!_isInteractiveCLIApp &&
                (_googleCloudUsage & GoogleCloudUsageFlag.SecretManager) != 0)
            {
                // Construct our service provider and configuration source regardless
                // of whether we are in production to ensure that dependencies are satisifed.
                var minimalServices = new ServiceCollection();
                minimalServices.AddSingleton(env);
                AddDefaultLogging(env, minimalServices);
                minimalServices.AddSingleton<IGoogleServices, GoogleServices>();
                minimalServices.AddSingleton<IGoogleApiRetry, GoogleApiRetry>();
                minimalServices.AddSingleton<IManagedTracer, NullManagedTracer>();
                minimalServices.AddSecretManagerConfiguration(_requireGoogleCloudSecretManagerLoad);

                // @note: This service provider *MUST NOT* be disposed, as instances continue to use it
                // throughout the lifetime of the application, not just during configuration setup.
                var minimalServiceProvider = minimalServices.BuildServiceProvider();
                var minimalLogging = minimalServiceProvider.GetRequiredService<ILogger<BaseConfigurator<TBase>>>();
                foreach (var configurationSource in minimalServiceProvider.GetServices<IConfigurationSource>())
                {
                    if (env.IsProduction())
                    {
                        minimalLogging.LogInformation($"Adding '{configurationSource.GetType().FullName}' configuration source to configuration as this instance is running in production...");
                        config.Add(configurationSource);
                    }
                    else
                    {
                        minimalLogging.LogInformation($"Not adding '{configurationSource.GetType().FullName}' configuration source to configuration as this instance is not running in production.");
                    }
                }
            }

            if (_customConfigLayers != null)
            {
                _customConfigLayers(env, config);
            }

            config.AddEnvironmentVariables();
        }

        private static void AddDefaultLogging(IHostEnvironment hostEnvironment, IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddSingleLineConsoleFormatter(options =>
                {
                    options.OmitLogPrefix = false;
                    options.ColorBehavior = hostEnvironment.IsProduction() ? LoggerColorBehavior.Disabled : LoggerColorBehavior.Default;
                });
                builder.AddSingleLineConsole();
            });
        }

        protected virtual void PreStartupConfigureServices(IHostEnvironment hostEnvironment, IConfiguration configuration, IServiceCollection services)
        {
            if (!_isInteractiveCLIApp)
            {
                // Add default logging configuration.
                AddDefaultLogging(hostEnvironment, services);
            }

            // Add the core stuff that every application needs.
            if (_prefixProvider != null)
            {
                services.AddSingleton(typeof(IPrefixProvider), _prefixProvider);
            }
            services.AddSingleton(typeof(ICurrentTenantService), _currentTenantService);
            services.AddSingleton(sp => sp.GetServices<IPrefixProvider>().ToArray());

            if (_googleCloudUsage != GoogleCloudUsageFlag.None)
            {
                // Add global environment configuration.
                services.AddSingleton<IGoogleServices, GoogleServices>();
                services.AddSingleton<IGoogleApiRetry, GoogleApiRetry>();
            }

            // Add the cache services.
            services.AddMemoryCache();
            services.AddDistributedRedpointCache(hostEnvironment);

            // Add file storage.
            if (hostEnvironment.IsDevelopment() || hostEnvironment.IsStaging())
            {
                services.AddSingleton<IFileStorage, LocalFileStorage>();
            }
            else
            {
                services.AddSingleton<IFileStorage, B2NetFileStorage>();
            }

            // Add metrics and OpenTelemetry. Note that we always use the HTTP listener even for ASP.NET Core
            // because we want to guarantee that metrics can run on a different port without additional
            // ASP.NET Core configuration.
            services.AddMetrics();
            try
            {
                services.AddOpenTelemetry()
                    .WithMetrics(builder => builder
                        .AddMeter("*")
                        .AddPrometheusHttpListener(options =>
                        {
                            var prometheusPrefix = configuration["CloudFramework:Prometheus:HttpPrefix"];
                            if (!string.IsNullOrWhiteSpace(prometheusPrefix))
                            {
                                options.UriPrefixes = [prometheusPrefix];
                            }
                            // @note: Don't attempt to listen on * in Development, since we won't have permission on Windows.
                            else if (!hostEnvironment.IsDevelopment())
                            {
                                options.UriPrefixes = ["http://*:9464/"];
                            }
                        }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error: Failed to register Prometheus endpoint for metrics collection: {ex}");
            }
        }

        protected virtual void PostStartupConfigureServices(IServiceCollection services)
        {
            // Add the global services provided by Cloud Framework.

            if ((_googleCloudUsage & GoogleCloudUsageFlag.Datastore) != 0)
            {
                services.AddSingleton<IGlobalRepository, DatastoreGlobalRepository>();
                services.AddSingleton<IGlobalRepositoryHook[]>(svc => svc.GetServices<IGlobalRepositoryHook>().ToArray());
                services.AddSingleton<IModelConverter<string>, JsonModelConverter>();
                services.AddSingleton<IModelConverter<Entity>, EntityModelConverter>();
                services.AddSingleton<IExpressionConverter, DefaultExpressionConverter>();
                services.AddSingleton<IRedisCacheRepositoryLayer, RedisCacheRepositoryLayer>();
                services.AddSingleton<IDatastoreRepositoryLayer, DatastoreRepositoryLayer>();
                services.AddSingleton<IGlobalLockService, DatastoreBasedGlobalLockService>();
                if (!_isInteractiveCLIApp)
                {
                    services.AddHostedService<DatastoreStartupMigrator>();
                    services.AddTransient<RegisteredModelMigratorBase[]>(sp => sp.GetServices<RegisteredModelMigratorBase>().ToArray());
                }
                services.AddSingleton<IGlobalShardedCounter, DefaultGlobalShardedCounter>();
                services.AddSingleton<IDatastoreContentionRetry, DefaultDatastoreContentionRetry>();
            }
            if ((_googleCloudUsage & GoogleCloudUsageFlag.BigQuery) != 0)
            {
                services.AddSingleton<IBigQuery, DefaultBigQuery>();
            }
            if ((_googleCloudUsage & GoogleCloudUsageFlag.PubSub) != 0)
            {
                services.AddSingleton<IPubSub, GooglePubSub>();
            }
            else
            {
                services.AddSingleton<IPubSub, NullPubSub>();
            }
            if (!_isInteractiveCLIApp && (_googleCloudUsage & GoogleCloudUsageFlag.SecretManager) != 0)
            {
                services.AddSecretManagerRuntime();
            }

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

            if (services.Any(x => x.ServiceType == typeof(IQuartzScheduledProcessorBinding)))
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
}
