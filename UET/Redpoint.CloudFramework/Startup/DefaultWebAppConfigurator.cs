namespace Redpoint.CloudFramework.Startup
{
    using Counter;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Quartz;
    using Redpoint.CloudFramework.Abstractions;
    using Redpoint.CloudFramework.DataProtection;
    using Redpoint.CloudFramework.Locking;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Processor;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Datastore;
    using Redpoint.CloudFramework.Tracing;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

#pragma warning disable CS0612
    internal class DefaultWebAppConfigurator : BaseConfigurator<IWebAppConfigurator>, IWebAppConfigurator, IStartupConfigureServicesFilter
#pragma warning restore CS0612
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)]
        private Type? _startupType;
        private WebHostBuilderContext? _context;
        private double _tracingRate = 0.0;
        private Func<IConfiguration, string, DevelopmentDockerContainer[]>? _dockerFactory;
        private Func<IConfiguration, string, HelmConfiguration>? _helmConfig;
        private string[] _prefixes = Array.Empty<string>();
        private readonly Dictionary<string, Action<IServiceCollection>> _processors = new Dictionary<string, Action<IServiceCollection>>();

        public IWebAppConfigurator UseStartup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] T>()
        {
            _startupType = typeof(T);
            return this;
        }

        public IWebAppConfigurator AddDevelopmentProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IContinuousProcessor
        {
            _processors[T.RoleName] = (services) =>
            {
                services.AddTransient<T>();
                services.AddHostedService<ContinuousProcessorHostedService<T>>();
            };
            return this;
        }

        public IWebAppConfigurator AddDevelopmentProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(Action<TriggerBuilder> triggerBuilder) where T : class, IScheduledProcessor
        {
            _processors[T.RoleName] = (services) =>
            {
                services.AddTransient<T>();
                services.AddTransient<IQuartzScheduledProcessorBinding>(_ =>
                {
                    return new QuartzScheduledProcessorBinding<T>(T.RoleName, triggerBuilder);
                });
            };
            return this;
        }

        public IWebAppConfigurator UseSentryTracing(double tracingRate)
        {
            _tracingRate = tracingRate;
            return this;
        }

        public IWebAppConfigurator UsePerformanceTracing(double tracingRate)
        {
            return UseSentryTracing(tracingRate);
        }

        public IWebAppConfigurator UseDevelopmentDockerContainers(Func<IConfiguration, string, DevelopmentDockerContainer[]> factory)
        {
            _dockerFactory = factory;
            return this;
        }

        public IWebAppConfigurator UseHelm(Func<IConfiguration, string, HelmConfiguration> helmConfig)
        {
            _helmConfig = helmConfig;
            return this;
        }

        public IWebAppConfigurator FilterPathPrefixesFromSentryPerformance(string[] prefixes)
        {
            _prefixes = prefixes ?? Array.Empty<string>();
            return this;
        }

        public Task<IWebHost> GetWebApp()
        {
            ValidateConfiguration();
            if (_startupType == null)
            {
                throw new InvalidOperationException("You must specify the ASP.NET startup class by calling UseStartup<T>().");
            }
            if (typeof(IStartup).IsAssignableFrom(_startupType))
            {
                throw new InvalidOperationException("Your startup class must not implement IStartup (instead, use convention-based startup).");
            }

            var hostBuilder = new WebHostBuilder()
                .ConfigureServices((context, services) =>
                {
                    if (_helmConfig == null)
                    {
                        // Add the lifetime service that will set up the development environment if necessary.
                        services.AddSingleton<DevelopmentStartup, DevelopmentStartup>(sp =>
                        {
                            return new DevelopmentStartup(
                                sp.GetRequiredService<IHostEnvironment>(),
                                sp.GetRequiredService<ILogger<DevelopmentStartup>>(),
                                _googleCloudUsage,
                                sp.GetRequiredService<IConfiguration>(),
                                _dockerFactory);
                        });
                        services.AddSingleton<IHostedService, DevelopmentStartup>(sp =>
                        {
                            return sp.GetRequiredService<DevelopmentStartup>();
                        });
                    }
                    else if (context.HostingEnvironment.IsDevelopment())
                    {
                        var helmConfig = _helmConfig(context.Configuration, context.HostingEnvironment.ContentRootPath);
                        services.AddSingleton<IOptionalHelmConfiguration>(new BoundHelmConfiguration(helmConfig));
                        Environment.SetEnvironmentVariable("REDIS_SERVER", "localhost:" + helmConfig.RedisPort);
                    }
                })
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseSentry(options =>
                {
                    options.TracesSampleRate = _tracingRate;
                    options.TracesSampler = (ctx) =>
                    {
                        if (ctx.CustomSamplingContext.ContainsKey("__HttpPath") &&
                            ctx.CustomSamplingContext["__HttpPath"] is string)
                        {
                            var path = (string?)ctx.CustomSamplingContext["__HttpPath"];
                            if (path != null)
                            {
                                if (path == "/healthz")
                                {
                                    return 0;
                                }

                                if (_prefixes.Any(x => path.StartsWith(x, StringComparison.Ordinal)))
                                {
                                    return 0;
                                }
                            }
                        }

                        return null;
                    };
                    options.AdjustStandardEnvironmentNameCasing = false;
                })
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    ConfigureAppConfiguration(hostingContext.HostingEnvironment, config);
                })
                .ConfigureServices((context, services) =>
                {
                    var configurationBuilder = new ConfigurationBuilder()
                            .SetBasePath(context.HostingEnvironment.ContentRootPath);
                    ConfigureAppConfiguration(context.HostingEnvironment, configurationBuilder);
                    var configuration = configurationBuilder.Build();
                    // Register IConfiguration for web host.
                    services.AddSingleton<IConfiguration>(configuration);
                    // Replace the builder's IConfiguration for the rest of ConfigureServices and Startup.
                    context.Configuration = configuration;
                })
                .ConfigureServices((context, services) =>
                {
                    _context = context;
                    // There is no replacement for this functionality, but UseStartup does not immediately execute so calling ConfigureServices on the host builder does not allow us to execute "post startup" configure, and there's no other way of hooking around startup.
#pragma warning disable CS0612
                    services.AddSingleton<IStartupConfigureServicesFilter>(this);
#pragma warning restore CS0612
                })
                .UseStartup(_startupType);
            return Task.FromResult(hostBuilder.Build());
        }

        public async Task StartWebApp()
        {
            var host = await GetWebApp().ConfigureAwait(false);
            await host.RunAsync().ConfigureAwait(false);
        }

        public async Task StartWebApp<T>() where T : IWebAppProvider
        {
            var host = await T.GetWebHostAsync().ConfigureAwait(false);
            await host.RunAsync().ConfigureAwait(false);
        }

        public async Task StartWebApp(IWebHost host)
        {
            ArgumentNullException.ThrowIfNull(host);
            await host.RunAsync().ConfigureAwait(false);
        }

        Action<IServiceCollection> IStartupConfigureServicesFilter.ConfigureServices(Action<IServiceCollection> next)
        {
            return services =>
            {
                this.PreStartupConfigureServices(_context!.HostingEnvironment, services);

                next(services);

                DefaultWebAppConfigurator.RemoveDefaultDataProtectionServices(services);
                this.PostStartupConfigureServices(services);

                if (_context.HostingEnvironment.IsDevelopment() && _processors.Count > 0)
                {
                    foreach (var kv in _processors)
                    {
                        kv.Value(services);
                    }
                }
            };
        }

        private static void RemoveDefaultDataProtectionServices(IServiceCollection services)
        {
            // AddSession in .NET 5 automatically calls AddDataProtection. This will register a service for IConfigureOptions, which in turn resolves IRegistryPolicyResolver which then goes through and sets up the default data protection. We never want to use these services; we only ever want to use our DataProtectionProvider, so undo all of the bindings that AddDataProtectionServices has gone and set up.
            void RemoveSingleBoundService(IServiceCollection services, string fullName)
            {
                foreach (var serviceDescriptor in services.Where(x => x?.ServiceType?.FullName == fullName).ToList())
                {
                    services.Remove(serviceDescriptor);
                }
            }
            void RemoveSingleBoundImplementation(IServiceCollection services, string fullName)
            {
                foreach (var serviceDescriptor in services.Where(x => x?.ImplementationType?.FullName == fullName).ToList())
                {
                    services.Remove(serviceDescriptor);
                }
            }
            RemoveSingleBoundService(services, "Microsoft.AspNetCore.DataProtection.IRegistryPolicyResolver");
            RemoveSingleBoundImplementation(services, "Microsoft.AspNetCore.DataProtection.Internal.KeyManagementOptionsSetup");
            RemoveSingleBoundImplementation(services, "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionOptionsSetup");
            RemoveSingleBoundService(services, "Microsoft.AspNetCore.DataProtection.KeyManagement.IKeyManager");
            RemoveSingleBoundService(services, "Microsoft.AspNetCore.DataProtection.Infrastructure.IApplicationDiscriminator");
            RemoveSingleBoundImplementation(services, "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService");
            RemoveSingleBoundService(services, "Microsoft.AspNetCore.DataProtection.KeyManagement.Internal.IDefaultKeyResolver");
            RemoveSingleBoundService(services, "Microsoft.AspNetCore.DataProtection.KeyManagement.Internal.IKeyRingProvider");
            foreach (var serviceDescriptor in services.Where(x => x?.ServiceType == typeof(IDataProtectionProvider) && x?.ImplementationType != typeof(StaticDataProtectionProvider)).ToList())
            {
                services.Remove(serviceDescriptor);
            }
            RemoveSingleBoundService(services, "Microsoft.AspNetCore.DataProtection.XmlEncryption.ICertificateResolver");
        }

        protected override void PreStartupConfigureServices(IHostEnvironment hostEnvironment, IServiceCollection services)
        {
            // Add the static data protector.
            services.AddSingleton<IDataProtectionProvider, StaticDataProtectionProvider>();
            services.AddSingleton<IDataProtector, StaticDataProtector>();

            base.PreStartupConfigureServices(hostEnvironment, services);
        }

        protected override void PostStartupConfigureServices(IServiceCollection services)
        {
            // Add common HTTP services.
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<IActionContextAccessor, ActionContextAccessor>();

            base.PostStartupConfigureServices(services);

            // Add the services for multi-tenanting. These are only valid in a web app, and we only register them if the current tenant service is not our builtin SingleTenantService.
            if (_currentTenantService != typeof(SingleCurrentTenantService))
            {
                if ((_googleCloudUsage & GoogleCloudUsageFlag.Datastore) != 0)
                {
                    services.AddScoped<IRepository, DatastoreRepository>();
                    services.AddScoped<ILockService, DefaultLockService>();
                    services.AddScoped<IShardedCounter, DefaultShardedCounter>();
                }
                services.AddScoped<IPrefix, DefaultPrefix>();
            }

            // Register the Sentry tracer, since we always use Sentry.
            services.AddSingleton<IManagedTracer, SentryManagedTracer>();

            // If we don't have the HTTP client factory registered, register it now.
            if (!services.Any(x => x.ServiceType == typeof(IHttpClientFactory)))
            {
                services.AddHttpClient();
            }
        }
    }
}
