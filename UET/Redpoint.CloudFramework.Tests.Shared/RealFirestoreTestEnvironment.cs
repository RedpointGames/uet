namespace Redpoint.CloudFramework.Tests
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Counter;
    using Redpoint.CloudFramework.Locking;
    using Redpoint.CloudFramework.Repository.Datastore;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Startup;
    using Redpoint.CloudFramework.Tracing;
    using StackExchange.Redis;
    using System;
    using Redpoint.CloudFramework.Prefix;

    public static class RealFirestoreTestEnvironment
    {
        public static ICloudFrameworkTestEnvironment CreateWithServices(
            Action<IServiceCollection> servicesFactory)
        {
            ArgumentNullException.ThrowIfNull(servicesFactory);

            var services = new ServiceCollection();

            if (Environment.GetEnvironmentVariable("GITLAB_CI") == "true")
            {
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                {
                    return new ConnectionMultiplexerProxy(
                        $"redis:6379",
                        sp.GetRequiredService<ILogger<ConnectionMultiplexerProxy>>());
                });
            }
            else
            {
                Environment.SetEnvironmentVariable("REDIS_SERVER", "localhost:61000");
            }

            var hostEnvironment = new TestRealFirestoreHostEnvironment();
            services.AddSingleton<IHostEnvironment>(hostEnvironment);
            services.AddSingleton<IConfiguration>(sp =>
            {
                return new ConfigurationBuilder().Build();
            });
            new Configurator().Configure(hostEnvironment, services);
            services.AddSingleton<IManagedTracer, NullManagedTracer>();

            // Add namespaced services.
            services.AddScoped<ICurrentTenantService, TestTenantService>();
            services.AddScoped<IRepository, DatastoreRepository>();
            services.AddScoped<ILockService, DefaultLockService>();
            services.AddScoped<IPrefix, DefaultPrefix>();
            services.AddSingleton<IGlobalShardedCounter, DefaultGlobalShardedCounter>();
            services.AddSingleton<IShardedCounter, DefaultShardedCounter>();

            services.AddHttpClient();

            servicesFactory(services);

#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
            return new NestedCloudFrameworkTestEnvironment(services.BuildServiceProvider());
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
        }
    }
}
