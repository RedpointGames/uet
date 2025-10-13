namespace Redpoint.CloudFramework.Tests
{
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using Google.Api.Gax;
    using Google.Cloud.Datastore.V1;
    using Grpc.Core;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Counter;
    using Redpoint.CloudFramework.GoogleInfrastructure;
    using Redpoint.CloudFramework.Locking;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Datastore;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Hooks;
    using Redpoint.CloudFramework.Repository.Transaction;
    using Redpoint.CloudFramework.Startup;
    using Redpoint.CloudFramework.Tracing;
    using StackExchange.Redis;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Sdk;
    using Redpoint.CloudFramework.Prefix;
    using System.Diagnostics;
    using Xunit.v3;

    public class CloudFrameworkTestEnvironment : CloudFrameworkTestEnvironment<DefaultCloudFrameworkTestEnvironmentConfiguration>
    {
        public CloudFrameworkTestEnvironment(IMessageSink messageSink) : base(messageSink)
        {
        }
    }

    public class CloudFrameworkTestEnvironment<TConfiguration> : IAsyncLifetime, ICloudFrameworkTestEnvironment where TConfiguration : ICloudFrameworkTestEnvironmentConfiguration, new()
    {
#pragma warning disable CS8618
        public CloudFrameworkTestEnvironment(
#pragma warning restore CS8618
            IMessageSink messageSink)
        {
            _messageSink = messageSink;
        }

        public IServiceProvider Services { get; private set; }

        public ICloudFrameworkTestEnvironment CreateWithServices()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit();
            });

            _messageSink.OnMessage(new DiagnosticMessage($"Building service provider"));

            if (Environment.GetEnvironmentVariable("GITLAB_CI") == "true")
            {
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                {
                    return new ConnectionMultiplexerProxy(
                        $"redis:6379",
                        sp.GetRequiredService<ILogger<ConnectionMultiplexerProxy>>());
                });
            }

            var hostEnvironment = new TestHostEnvironment();
            services.AddSingleton<IHostEnvironment>(hostEnvironment);
            services.AddSingleton<IConfiguration>(sp =>
            {
                return new ConfigurationBuilder().Build();
            });
            services.AddSingleton<IGlobalRepositoryHook, TestRepositoryHook>();
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

            new TConfiguration().RegisterServices(services);

#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
            return new NestedCloudFrameworkTestEnvironment(services.BuildServiceProvider());
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
        }

        private readonly IMessageSink _messageSink;

        public async ValueTask InitializeAsync()
        {
            if (Environment.GetEnvironmentVariable("IS_RUNNING_UNDER_CI") == "true")
            {
                // Redis set up as part of CreateWithServices.
                // PUBSUB_SERVER is set by CI script.
                // DATASTORE_SERVER is set by CI script.
            }
            else if (Process.GetProcessesByName("Rancher Desktop").Length != 0)
            {
                // Rancher Desktop doesn't work with the Docker C# library for some
                // reason; just expect things to have been manually started.
                Environment.SetEnvironmentVariable("RCF_DO_NOT_WAIT_FOR_SERVICES", "1");
                Environment.SetEnvironmentVariable("REDIS_SERVER", "127.0.0.1:61000");
                Environment.SetEnvironmentVariable("PUBSUB_SERVER", "127.0.0.1:61001");
                Environment.SetEnvironmentVariable("DATASTORE_SERVER", "127.0.0.1:61002");
            }
            else
            {
                // Spin up Redis and Datastore Emulator on the local Docker instance.
                var client = new DockerClientConfiguration().CreateClient();

                // Pull all images.
                var currentImages = await client.Images.ListImagesAsync(new ImagesListParameters
                {
                    All = true,
                }).ConfigureAwait(true);
                foreach (var expectedContainer in ContainerConfiguration._expectedContainers)
                {
                    _messageSink.OnMessage(new DiagnosticMessage($"Pulling image: {expectedContainer.image}"));
                    await client.Images.CreateImageAsync(new ImagesCreateParameters
                    {
                        FromImage = expectedContainer.image,
                    }, null, new NullLogProgress()).ConfigureAwait(true);
                }

                // Cleanup any old testing containers.
                var containers = await client.Containers.ListContainersAsync(new ContainersListParameters
                {
                    All = true,
                }).ConfigureAwait(true);
                foreach (var container in containers)
                {
                    if (container.Names.Any(x => x.StartsWith($"/rcftest", StringComparison.Ordinal) || x.StartsWith($"rcftest", StringComparison.Ordinal)) &&
                        (container.Status.Contains("hour", StringComparison.Ordinal) ||
                         container.Status.Contains("day", StringComparison.Ordinal)))
                    {
                        _messageSink.OnMessage(new DiagnosticMessage($"Stopping container: {container.ID}"));
                        await client.Containers.StopContainerAsync(container.ID, new ContainerStopParameters
                        {
                            WaitBeforeKillSeconds = 0,
                        }).ConfigureAwait(true);
                        _messageSink.OnMessage(new DiagnosticMessage($"Removing container: {container.ID}"));
                        await client.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters
                        {
                            Force = true,
                        }).ConfigureAwait(true);
                    }
                }

                // Create containers if they don't exist.
                containers = await client.Containers.ListContainersAsync(new ContainersListParameters
                {
                    All = true,
                }).ConfigureAwait(true);
                foreach (var expectedContainer in ContainerConfiguration._expectedContainers)
                {
                    if (!containers.Any(x => x.Names.Any(y => y.EndsWith(expectedContainer.name, StringComparison.Ordinal))))
                    {
                        _messageSink.OnMessage(new DiagnosticMessage($"Creating container: {expectedContainer.name}"));
                        var createdContainerConfig = new CreateContainerParameters
                        {
                            Name = expectedContainer.name,
                            Image = expectedContainer.image,
                            Cmd = expectedContainer.arguments,
                            ExposedPorts = new Dictionary<string, EmptyStruct>
                            {
                                { $"{expectedContainer.port}/tcp", new EmptyStruct() },
                            },
                            HostConfig = new HostConfig
                            {
                                PortBindings = new Dictionary<string, IList<PortBinding>>
                                {
                                    {
                                        $"{expectedContainer.port}/tcp",
                                        new List<PortBinding>
                                        {
                                            new PortBinding
                                            {
                                                HostIP = "0.0.0.0",
                                                HostPort = null,
                                            }
                                        }
                                    },
                                },
                            },
                            Env = new List<string>
                            {
                                "CLOUDSDK_CORE_PROJECT=local-dev"
                            },
                        };
                        var createdContainer = await client.Containers.CreateContainerAsync(createdContainerConfig).ConfigureAwait(true);

                        _messageSink.OnMessage(new DiagnosticMessage($"Starting container: {createdContainer.ID}"));
                        await client.Containers.StartContainerAsync(createdContainer.ID, new ContainerStartParameters
                        {
                        }).ConfigureAwait(true);
                    }
                }

                // Now get the ports they were mapped to on the host.
                containers = await client.Containers.ListContainersAsync(new ContainersListParameters
                {
                    All = true,
                }).ConfigureAwait(true);
                var didSetRedis = false;
                var didSetPubsub = false;
                var didSetDatastore = false;
                foreach (var expectedContainer in ContainerConfiguration._expectedContainers)
                {
                    var container = containers.FirstOrDefault(x => x.Names.Any(y => y.EndsWith(expectedContainer.name, StringComparison.Ordinal)));
                    if (container == null)
                    {
                        _messageSink.OnMessage(new DiagnosticMessage($"Unable to locate container with name: {expectedContainer.name}"));
                        throw new InvalidOperationException("Failed to start required container for tests.");
                    }

                    var targetPort = container.Ports.First(x => x.PrivatePort == expectedContainer.port);
                    var targetConnection = $"localhost:{targetPort.PublicPort}";
                    switch (expectedContainer.type)
                    {
                        case "redis":
                            Environment.SetEnvironmentVariable("REDIS_SERVER", targetConnection);
                            didSetRedis = true;
                            break;
                        case "pubsub":
                            Environment.SetEnvironmentVariable("PUBSUB_SERVER", targetConnection);
                            didSetPubsub = true;
                            break;
                        case "datastore":
                            Environment.SetEnvironmentVariable("DATASTORE_SERVER", targetConnection);
                            didSetDatastore = true;
                            break;
                    }
                }
                if (!didSetRedis || !didSetPubsub || !didSetDatastore)
                {
                    throw new InvalidOperationException("Could not set test environment up correctly based on containers.");
                }
            }

            Services = CreateWithServices().Services;

            _messageSink.OnMessage(new DiagnosticMessage($"Waiting for Datastore to be operational..."));

            // Wait for Datastore to be operational.
            var deadline = DateTime.UtcNow.AddSeconds(30);
            for (int i = 0; i < 30; i++)
            {
                try
                {
                    var googleServices = Services.GetRequiredService<IGoogleServices>();
                    var datastore = googleServices.Build<DatastoreClient, DatastoreClientBuilder>(
                    DatastoreClient.DefaultEndpoint,
                    DatastoreClient.DefaultScopes);
                    var txn = await datastore.BeginTransactionAsync(new BeginTransactionRequest
                    {
                        ProjectId = "local-dev",
                    }, new Google.Api.Gax.Grpc.CallSettings(null, Expiration.FromTimeout(TimeSpan.FromMilliseconds(100)), null, null, null, null)).ConfigureAwait(true);
                    await datastore.RollbackAsync(new RollbackRequest
                    {
                        ProjectId = "local-dev",
                        Transaction = txn.Transaction,
                    }, new Google.Api.Gax.Grpc.CallSettings(null, Expiration.FromTimeout(TimeSpan.FromMilliseconds(100)), null, null, null, null)).ConfigureAwait(true);
                    break;
                }
                catch (RpcException ex) when (
                    ex.StatusCode == StatusCode.Unavailable ||
                    ex.StatusCode == StatusCode.DeadlineExceeded ||
                    ex.StatusCode == StatusCode.Cancelled)
                {
                    if (DateTime.UtcNow > deadline || Environment.GetEnvironmentVariable("RCF_DO_NOT_WAIT_FOR_SERVICES") == "1")
                    {
                        _messageSink.OnMessage(new DiagnosticMessage($"Ran out of time waiting for Datastore to become ready."));
                        break;
                    }
                    else
                    {
                        _messageSink.OnMessage(new DiagnosticMessage($"Datastore not ready yet on attempt #{i + 1}."));
                        await Task.Delay(500).ConfigureAwait(true);
                        continue;
                    }
                }
            }

            _messageSink.OnMessage(new DiagnosticMessage($"Starting hosted services..."));

            foreach (var hostedService in Services.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(true);
            }

            _messageSink.OnMessage(new DiagnosticMessage($"Tests are ready to execute."));
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            _messageSink.OnMessage(new DiagnosticMessage($"Stopping hosted services..."));

            foreach (var hostedService in Services.GetServices<IHostedService>())
            {
                await hostedService.StopAsync(CancellationToken.None).ConfigureAwait(true);
            }

            if (Environment.GetEnvironmentVariable("GITLAB_CI") == "true")
            {
                // No cleanup operations here.
            }
            else
            {
                // Shutdown containers on the local Docker instance.
                var client = new DockerClientConfiguration().CreateClient();

                // Cleanup containers if they exist.
                /*
                var containers = await client.Containers.ListContainersAsync(new Docker.DotNet.Models.ContainersListParameters
                {
                    All = true,
                });
                foreach (var expectedContainer in _expectedContainers)
                {
                    var container = containers.FirstOrDefault(x => x.Names.Any(y => y.EndsWith(expectedContainer.name)));
                    if (container != null)
                    {
                        _messageSink.OnMessage(new DiagnosticMessage($"Stopping container: {container.ID}"));
                        await client.Containers.StopContainerAsync(container.ID, new ContainerStopParameters
                        {
                            WaitBeforeKillSeconds = 0,
                        });
                        _messageSink.OnMessage(new DiagnosticMessage($"Removing container: {container.ID}"));
                        await client.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters
                        {
                            Force = true,
                        });
                    }
                }
                */
            }

            _messageSink.OnMessage(new DiagnosticMessage($"Tests execution has finished."));
        }

        private class TestRepositoryHook : IGlobalRepositoryHook
        {
            public Task MutateEntityBeforeWrite(string @namespace, Entity entity)
            {
                return Task.CompletedTask;
            }

            public Task PostCreate<T>(string @namespace, T model, IModelTransaction? transaction) where T : class, IModel, new()
            {
                return Task.CompletedTask;
            }

            public Task PostDelete<T>(string @namespace, T model, IModelTransaction? transaction) where T : class, IModel, new()
            {
                return Task.CompletedTask;
            }

            public Task PostUpdate<T>(string @namespace, T model, IModelTransaction? transaction) where T : class, IModel, new()
            {
                return Task.CompletedTask;
            }

            public Task PostUpsert<T>(string @namespace, T model, IModelTransaction? transaction) where T : class, IModel, new()
            {
                return Task.CompletedTask;
            }
        }
    }
}
