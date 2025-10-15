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
                config.AddGoogleCloudSecretManager(env);
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
                services.AddCloudFrameworkGoogleCloud();
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
                if (!hostEnvironment.IsDevelopment())
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
                            }));
                }
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
                services.AddCloudFrameworkRepository(
                    enableMigrations: !_isInteractiveCLIApp,
                    enableRedis: true);
            }
            if ((_googleCloudUsage & GoogleCloudUsageFlag.BigQuery) != 0)
            {
                services.AddSingleton<IBigQuery, DefaultBigQuery>();
            }
            if (!_isInteractiveCLIApp && (_googleCloudUsage & GoogleCloudUsageFlag.SecretManager) != 0)
            {
                services.AddSecretManagerRuntime();
            }

            services.AddCloudFrameworkCore();

            if (services.Any(x => x.ServiceType == typeof(IQuartzScheduledProcessorBinding)))
            {
                services.AddCloudFrameworkQuartz();
            }
        }
    }
}
