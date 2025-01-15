namespace Redpoint.CloudFramework
{
    using Redpoint.CloudFramework.Cache;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using StackExchange.Redis;
    using Microsoft.Extensions.Logging;
    using System.Threading;
    using Microsoft.Extensions.Caching.StackExchangeRedis;
    using Microsoft.Extensions.Hosting;
    using System.Threading.Tasks;
    using Redpoint.CloudFramework.Repository.Redis;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Startup;

    internal static class RedisConnector
    {
        internal static void AddDistributedRedpointCache(
            this IServiceCollection services,
            IHostEnvironment hostEnvironment)
        {
            var redisServerEnv = Environment.GetEnvironmentVariable("REDIS_SERVER");
            string redisServer;
            if (!hostEnvironment.IsDevelopment())
            {
                if (string.IsNullOrWhiteSpace(redisServerEnv))
                {
                    throw new InvalidOperationException("Cloud Framework requires a Redis server in production/staging environments. Set the REDIS_SERVER environment variable.");
                }
                redisServer = redisServerEnv;
            }
            else if (!string.IsNullOrWhiteSpace(redisServerEnv))
            {
                // Allow development override for cases where this application is being
                // run as a dependency of another application.
                redisServer = redisServerEnv;
            }
            else if (Environment.GetEnvironmentVariable("GITLAB_CI") == "true")
            {
                // This will be running a service in GitLab CI/CD.
                redisServer = "redis:6379";
            }
            else
            {
                // This will be running in a Docker container on the local machine.
                redisServer = "localhost:6379";
            }

            var redisConnect = ConnectionMultiplexerProxy.GetRedisConnectionString(redisServer);

            services.AddOptions();
            services.Configure<RedisCacheOptions>(x =>
            {
                x.Configuration = redisConnect;
            });
            services.Add(ServiceDescriptor.Singleton<IDistributedCache, RetryableRedisCache>());
            services.Add(ServiceDescriptor.Singleton<IDistributedCacheExtended, DistributedCacheExtended>());

            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                return new ConnectionMultiplexerProxy(
                    redisServer,
                    sp.GetRequiredService<ILogger<ConnectionMultiplexerProxy>>());
            });
            services.AddSingleton<IHostedService, WaitUntilRedisConnectedService>();
        }

        private class WaitUntilRedisConnectedService : IHostedService
        {
            private readonly IServiceProvider _serviceProvider;

            public WaitUntilRedisConnectedService(IServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                // This will block until we are connected to Redis. Since the web host's IHostedService won't have StartAsync called until this StartAsync returns, this ensures we don't start listening until we're connected to Redis.
                _ = _serviceProvider.GetRequiredService<IConnectionMultiplexer>().ClientName;
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
