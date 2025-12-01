namespace Redpoint.CloudFramework.Startup
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.CloudFramework.Processor;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Tracing;
    using System.Diagnostics.CodeAnalysis;
    using Quartz;
    using OpenTelemetry.Metrics;

    internal class DefaultServiceAppConfigurator : BaseConfigurator<IServiceAppConfigurator>, IServiceAppConfigurator
    {
        private readonly Dictionary<string, Action<IServiceCollection>> _processors = new Dictionary<string, Action<IServiceCollection>>();
        private Func<IConfiguration, string, DevelopmentDockerContainer[]>? _dockerFactory;
        private Action<IServiceCollection>? _serviceConfiguration;
        private Func<IConfiguration, string, HelmConfiguration>? _helmConfig;
        private string[] _defaultRoleNames = Array.Empty<string>();

        public IServiceAppConfigurator AddProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IContinuousProcessor
        {
            _processors[T.RoleName] = (services) =>
            {
                services.AddTransient<T>();
                services.AddHostedService<ContinuousProcessorHostedService<T>>();
            };
            return this;
        }

        public IServiceAppConfigurator AddProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(Action<TriggerBuilder> triggerBuilder) where T : class, IScheduledProcessor
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

        public IServiceAppConfigurator UseDevelopmentDockerContainers(Func<IConfiguration, string, DevelopmentDockerContainer[]> factory)
        {
            _dockerFactory = factory;
            return this;
        }

        public IServiceAppConfigurator UseHelm(Func<IConfiguration, string, HelmConfiguration> helmConfig)
        {
            _helmConfig = helmConfig;
            return this;
        }

        public IServiceAppConfigurator UseDefaultRoles(params string[] roleNames)
        {
            _defaultRoleNames = roleNames;
            return this;
        }

        [RequiresDynamicCode("This internally uses HostBuilder, which requires dynamic code.")]
        public async Task<int> StartServiceApp(string[] args)
        {
            ValidateConfiguration();
            if (args.Contains("--help"))
            {
                Console.WriteLine("Specify one or more of the following roles on the command-line, or pass --all-roles:");
                foreach (var processor in _processors)
                {
                    Console.WriteLine("  " + processor.Key);
                }
                return 2;
            }

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
            {
                throw new InvalidOperationException("ASPNETCORE_ENVIRONMENT must set, even for service applications.");
            }

            var selectedRoleNames = new HashSet<string>();
            foreach (var processor in _processors)
            {
                if (args.Contains("--all-roles") ||
                    args.Contains(processor.Key))
                {
                    selectedRoleNames.Add(processor.Key);
                }
            }
            if (selectedRoleNames.Count == 0)
            {
                foreach (var defaultRoleName in _defaultRoleNames)
                {
                    if (_processors.ContainsKey(defaultRoleName))
                    {
                        selectedRoleNames.Add(defaultRoleName);
                    }
                }
            }
            if (selectedRoleNames.Count == 0)
            {
                throw new InvalidOperationException("No processors were enabled. Use --all-roles or list the roles to run on the command-line.");
            }

            var build = new HostBuilder()
                .UseEnvironment(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!)
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
                    if (_helmConfig == null)
                    {
                        // Add the lifetime service that will set up the development environment if necessary.
                        services.AddSingleton<IHostedService, DevelopmentStartup>(sp =>
                        {
                            return new DevelopmentStartup(
                                sp.GetRequiredService<IHostEnvironment>(),
                                sp.GetRequiredService<ILogger<DevelopmentStartup>>(),
                                _googleCloudUsage,
                                sp.GetRequiredService<IConfiguration>(),
                                _dockerFactory);
                        });
                    }
                    else if (context.HostingEnvironment.IsDevelopment())
                    {
                        var helmConfig = _helmConfig(context.Configuration, context.HostingEnvironment.ContentRootPath);
                        services.AddSingleton<IOptionalHelmConfiguration>(new BoundHelmConfiguration(helmConfig));
                        Environment.SetEnvironmentVariable("REDIS_SERVER", "localhost:" + helmConfig.RedisPort);
                    }
                })
                .ConfigureServices((context, services) =>
                {
                    if (IsUsingOtelTracing)
                    {
                        services.AddSingleton<IManagedTracer, OtelManagedTracer>();
                    }
                    else
                    {
                        services.AddSingleton<IManagedTracer, NullManagedTracer>();
                    }

                    this.PreStartupConfigureServices(context.HostingEnvironment, context.Configuration, services);
                    this._serviceConfiguration?.Invoke(services);
                    this.PostStartupConfigureServices(services);

                    foreach (var roleName in selectedRoleNames)
                    {
                        _processors[roleName](services);
                    }
                })
                .Build();
            await build.RunAsync().ConfigureAwait(false);
            return 0;
        }

        public IServiceAppConfigurator UseServiceConfiguration(Action<IServiceCollection> configureServices)
        {
            _serviceConfiguration = configureServices;
            return this;
        }
    }
}
