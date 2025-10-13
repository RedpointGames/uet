namespace Redpoint.CloudFramework.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Threading.Tasks;
    using Xunit;
    using Redpoint.CloudFramework.Configuration;
    using Redpoint.CloudFramework.GoogleInfrastructure;
    using Redpoint.CloudFramework.Tracing;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.FileProviders;
    using Google.Cloud.SecretManager.V1;
    using Grpc.Core;
    using Redpoint.CloudFramework.Startup;
    using Google.Apis.Auth.OAuth2.Responses;
    using Redpoint.Concurrency;
    using Google.Protobuf;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Extensions.Configuration;
    using System.Runtime.InteropServices;
    using System.Reflection;

    public class SecretManagerTests
    {
        private const int _pubSubWaitMilliseconds = 500;
        private const int _pubSubWaitIteration = 120000 / _pubSubWaitMilliseconds;

        private class DummyHostEnvironment : IHostEnvironment
        {
            public string ApplicationName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public IFileProvider ContentRootFileProvider { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public string ContentRootPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public string EnvironmentName { get => "Production"; set => throw new NotImplementedException(); }
        }

        private class RandomSecretManagerNotificationSuffixProvider : ISecretManagerNotificationSuffixProvider
        {
            [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Not used for security.")]
            public RandomSecretManagerNotificationSuffixProvider()
            {
                Suffix = $"automation-{Random.Shared.NextInt64()}";
            }

            public string Suffix { get; private init; }
        }

        private class IntegrationGoogleProjectIdProvider : IGoogleProjectIdProvider
        {
            public string ProjectId => "cloud-framework-unit-tests";
        }

        private static ServiceProvider CreateServiceProvider(bool isolatedNotificationManager = true)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment, DummyHostEnvironment>();
            services.AddLogging();
            services.AddSingleton<IGoogleProjectIdProvider, IntegrationGoogleProjectIdProvider>();
            services.AddSingleton<IGoogleServices, GoogleServices>();
            services.AddSingleton<IGoogleApiRetry, GoogleApiRetry>();
            services.AddSingleton<IManagedTracer, NullManagedTracer>();
            services.AddSingleton<ISecretManagerNotificationSuffixProvider, RandomSecretManagerNotificationSuffixProvider>();
            services.AddSecretManagerConfiguration(true, "test-secret", isolatedNotificationManager);
            services.AddHostedService<SecretManagerSubscriptionCleanupHostedService>();

            var serviceProvider = services.BuildServiceProvider();

            // Check that the execution environment has access to the test project, otherwise skip the test.
            var googleServices = serviceProvider.GetRequiredService<IGoogleServices>();
            try
            {
                var secretManager = googleServices.Build<SecretManagerServiceClient, SecretManagerServiceClientBuilder>(
                    SecretManagerServiceClient.DefaultEndpoint,
                    SecretManagerServiceClient.DefaultScopes);
                secretManager.ListSecrets(new ListSecretsRequest());
            }
            catch (RpcException ex) when (
                ex.StatusCode == StatusCode.Unauthenticated ||
                (ex.StatusCode == StatusCode.Internal && ex.InnerException is TokenResponseException ter && ter.Error.Error == "invalid_grant"))
            {
                Assert.Skip("The execution environment does not have application default credentials to access the Google Cloud test project.");
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("GOOGLE_APPLICATION_CREDENTIALS", StringComparison.Ordinal) ||
                ex.Message.Contains("Application Default Credentials", StringComparison.Ordinal))
            {
                Assert.Skip("The execution environment does not have application default credentials to access the Google Cloud test project.");
            }
            catch (TargetInvocationException tex) when (
                tex.InnerException != null &&
                (tex.InnerException.Message.Contains("GOOGLE_APPLICATION_CREDENTIALS", StringComparison.Ordinal) ||
                 tex.InnerException.Message.Contains("Application Default Credentials", StringComparison.Ordinal)))
            {
                Assert.Skip("The execution environment does not have application default credentials to access the Google Cloud test project.");
            }
            return serviceProvider;
        }

        [Fact]
        public void ConfigurationSourceBehaviour()
        {
            var sp = CreateServiceProvider();

            var csb = sp.GetRequiredService<ISecretManagerConfigurationSourceBehaviour>();
            Assert.True(csb.RequireSuccessfulLoad);
        }

        [Fact]
        public void TryGetSecret()
        {
            var sp = CreateServiceProvider();

            var secretAccess = sp.GetRequiredService<ISecretManagerAccess>();
            Assert.NotNull(secretAccess.TryGetSecret("test-secret"));
        }

        [Fact]
        public void TryGetLatestSecretVersion()
        {
            var sp = CreateServiceProvider();

            var secretAccess = sp.GetRequiredService<ISecretManagerAccess>();
            var secret = secretAccess.TryGetSecret("test-secret");
            Assert.NotNull(secret);
            var secretVersion = secretAccess.TryGetLatestSecretVersion(secret);
            Assert.NotNull(secretVersion);
            Assert.Equal(SecretVersion.Types.State.Enabled, secretVersion.State);
        }

        [Fact]
        public void TryAccessSecretVersion()
        {
            var sp = CreateServiceProvider();

            var secretAccess = sp.GetRequiredService<ISecretManagerAccess>();
            var secret = secretAccess.TryGetSecret("test-secret");
            Assert.NotNull(secret);
            var secretVersion = secretAccess.TryGetLatestSecretVersion(secret);
            Assert.NotNull(secretVersion);
            Assert.Equal(SecretVersion.Types.State.Enabled, secretVersion.State);
            var accessed = secretAccess.TryAccessSecretVersion(secretVersion);
            Assert.NotNull(accessed);
        }

        [Fact]
        public async Task TryGetLatestSecretVersionAsync()
        {
            var sp = CreateServiceProvider();

            var secretAccess = sp.GetRequiredService<ISecretManagerAccess>();
            var secret = secretAccess.TryGetSecret("test-secret");
            Assert.NotNull(secret);
            var secretVersion = await secretAccess.TryGetLatestSecretVersionAsync(secret).ConfigureAwait(false);
            Assert.NotNull(secretVersion);
            Assert.Equal(SecretVersion.Types.State.Enabled, secretVersion.State);
        }

        [Fact]
        public async Task TryAccessSecretVersionAsync()
        {
            var sp = CreateServiceProvider();

            var secretAccess = sp.GetRequiredService<ISecretManagerAccess>();
            var secret = secretAccess.TryGetSecret("test-secret");
            Assert.NotNull(secret);
            var secretVersion = await secretAccess.TryGetLatestSecretVersionAsync(secret).ConfigureAwait(false);
            Assert.NotNull(secretVersion);
            Assert.Equal(SecretVersion.Types.State.Enabled, secretVersion.State);
            var accessed = await secretAccess.TryAccessSecretVersionAsync(secretVersion).ConfigureAwait(false);
            Assert.NotNull(accessed);
        }

        [Fact]
        public async Task Subscribe()
        {
            await using (CreateServiceProvider().AsAsyncDisposable(out var sp).ConfigureAwait(false))
            {
                var secretAccess = sp.GetRequiredService<ISecretManagerAccess>();
                var secret = secretAccess.TryGetSecret("test-secret");
                Assert.NotNull(secret);

                var secretNotifications = sp.GetRequiredService<ISecretManagerNotificationManager>();
                await secretNotifications.SubscribeAsync(secret).ConfigureAwait(false);

                var notified = false;
                secretNotifications.OnSecretUpdated.Add((secret, _) =>
                {
                    notified = true;
                    return Task.FromResult(Google.Cloud.PubSub.V1.SubscriberClient.Reply.Ack);
                });

                // Add a new secret version.
                var newVersion = await secretAccess.SecretClient.AddSecretVersionAsync(new AddSecretVersionRequest
                {
                    ParentAsSecretName = secret.SecretName,
                    Payload = new SecretPayload
                    {
                        Data = ByteString.CopyFromUtf8(
                            """
                            {
                                "Hello": "World2",
                            }
                            """),
                    }
                }).ConfigureAwait(false);

                // Destroy all old versions.
                await foreach (var version in secretAccess.SecretClient.ListSecretVersionsAsync(new ListSecretVersionsRequest
                {
                    Filter = "state:(ENABLED)",
                    ParentAsSecretName = secret.SecretName,
                }).ConfigureAwait(false))
                {
                    if (version.SecretVersionName != newVersion.SecretVersionName)
                    {
                        await secretAccess.SecretClient.DestroySecretVersionAsync(new DestroySecretVersionRequest
                        {
                            SecretVersionName = version.SecretVersionName,
                        }).ConfigureAwait(false);
                    }
                }

                // Give Pub/Sub some time to notify us.
                for (int i = 0; i < _pubSubWaitIteration; i++)
                {
                    if (notified)
                    {
                        break;
                    }
                    await Task.Delay(_pubSubWaitMilliseconds).ConfigureAwait(false);
                }
                Assert.True(notified);
            }
        }

        [Fact]
        [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Not used for security.")]
        public async Task AutoRefreshingSecret()
        {
            await using (CreateServiceProvider().AsAsyncDisposable(out var sp).ConfigureAwait(false))
            {
                var autoRefreshingFactory = sp.GetRequiredService<IAutoRefreshingSecretFactory>();

                await using (autoRefreshingFactory.Create("test-secret", true).AsAsyncDisposable(out var refreshingSecret).ConfigureAwait(false))
                {
                    Assert.NotNull(refreshingSecret);
                    Assert.IsType<DefaultAutoRefreshingSecret>(refreshingSecret);

                    var notified = false;
                    refreshingSecret.OnRefreshed = () =>
                    {
                        notified = true;
                    };

                    var secretAccess = sp.GetRequiredService<ISecretManagerAccess>();
                    var secret = secretAccess.TryGetSecret("test-secret");
                    Assert.NotNull(secret);

                    var generatedValue = $"{Random.Shared.NextInt64()}";

                    // Add a new secret version.
                    var newVersion = await secretAccess.SecretClient.AddSecretVersionAsync(new AddSecretVersionRequest
                    {
                        ParentAsSecretName = secret.SecretName,
                        Payload = new SecretPayload
                        {
                            Data = ByteString.CopyFromUtf8(
                                $$"""
                                {
                                    "Hello": "World2",
                                    "Test": "{{generatedValue}}",
                                }
                                """),
                        }
                    }).ConfigureAwait(false);

                    // Destroy all old versions.
                    await foreach (var version in secretAccess.SecretClient.ListSecretVersionsAsync(new ListSecretVersionsRequest
                    {
                        Filter = "state:(ENABLED)",
                        ParentAsSecretName = secret.SecretName,
                    }).ConfigureAwait(false))
                    {
                        if (version.SecretVersionName != newVersion.SecretVersionName)
                        {
                            await secretAccess.SecretClient.DestroySecretVersionAsync(new DestroySecretVersionRequest
                            {
                                SecretVersionName = version.SecretVersionName,
                            }).ConfigureAwait(false);
                        }
                    }

                    // Wait for the auto-refreshing secret to be notified.
                    for (int i = 0; i < _pubSubWaitIteration; i++)
                    {
                        if (notified)
                        {
                            break;
                        }
                        await Task.Delay(_pubSubWaitMilliseconds).ConfigureAwait(false);
                    }
                    Assert.True(notified);

                    // Ensure the auto-refreshing secret has the new value.
                    Assert.Equal(generatedValue, refreshingSecret.Data["Test"]);
                }
            }
        }

        [Fact]
        public async Task SecretManagerConfigurationProvider()
        {
            await using (CreateServiceProvider().AsAsyncDisposable(out var sp).ConfigureAwait(false))
            {
                var configurationSource = sp.GetRequiredService<IConfigurationSource>();
                var configurationProvider = configurationSource.Build(null! /* Not required for our implementation. */);
                Assert.NotNull(configurationProvider);

                configurationProvider.Load();

                var secretManagerConfigurationProvider = Assert.IsType<SecretManagerConfigurationProvider>(configurationProvider);
                Assert.NotNull(secretManagerConfigurationProvider._autoRefreshingSecret);
                Assert.IsType<DefaultAutoRefreshingSecret>(secretManagerConfigurationProvider._autoRefreshingSecret);

                Assert.True(configurationProvider.TryGet("Hello", out var value));
                Assert.Equal("World2", value);

                await sp.GetRequiredService<ISecretManagerNotificationManager>().UnsubscribeAllAsync().ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task SecretManagerSubscriptionCleanupHostedService()
        {
            await using (CreateServiceProvider().AsAsyncDisposable(out var sp).ConfigureAwait(false))
            {
                var hostedService = sp.GetServices<IHostedService>()
                    .OfType<SecretManagerSubscriptionCleanupHostedService>()
                    .FirstOrDefault();
                Assert.NotNull(hostedService);

                await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);
                await hostedService.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task ResolveNonIsolatedManager()
        {
            await using (CreateServiceProvider(false).AsAsyncDisposable(out var sp).ConfigureAwait(false))
            {
                sp.GetRequiredService<ISecretManagerNotificationManager>();
            }
        }
    }
}
