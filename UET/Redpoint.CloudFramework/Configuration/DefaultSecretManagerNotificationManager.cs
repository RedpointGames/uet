namespace Redpoint.CloudFramework.Configuration
{
    using Google.Api.Gax;
    using Google.Cloud.PubSub.V1;
    using Google.Cloud.SecretManager.V1;
    using Google.Protobuf.WellKnownTypes;
    using Grpc.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.GoogleInfrastructure;
    using System;
    using Topic = Google.Cloud.SecretManager.V1.Topic;

    internal class DefaultSecretManagerNotificationManager : ISecretManagerNotificationManager
    {
        private class SubscriptionState : IDisposable
        {
            public SubscriptionState(Secret secret, string subscriptionName)
            {
                Secret = secret;
                SubscriptionName = subscriptionName;
                SubscriptionCount = 0;
                Subscriber = null;
                CancellationTokenSource = new CancellationTokenSource();
                SubscriberInitTask = null;
                SubscriberRunTask = null;
            }

            public Secret Secret;
            public string SubscriptionName;
            public int SubscriptionCount;
            public SubscriberClient? Subscriber;
            public CancellationTokenSource CancellationTokenSource;
            public Task? SubscriberInitTask;
            public Task? SubscriberRunTask;

            public void Dispose()
            {
                CancellationTokenSource.Dispose();
            }
        }

        private readonly ILogger<DefaultSecretManagerNotificationManager> _logger;
        private readonly IGoogleServices _googleServices;
        private readonly Lazy<SubscriberServiceApiClient> _subscriberClient;
        private readonly Lazy<ChannelCredentials?> _subscriberCredential;
        private readonly Lazy<string?> _subscriberServiceEndpoint;
        private readonly Dictionary<string, SubscriptionState> _subscriptions;
        private readonly string? _subscriptionSuffix;

        public DefaultSecretManagerNotificationManager(
            ILogger<DefaultSecretManagerNotificationManager> logger,
            IGoogleServices googleServices,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _googleServices = googleServices;

            _subscriberClient = new Lazy<SubscriberServiceApiClient>(() => googleServices.Build<SubscriberServiceApiClient, SubscriberServiceApiClientBuilder>(
                SubscriberServiceApiClient.DefaultEndpoint,
                SubscriberServiceApiClient.DefaultScopes));
            _subscriberCredential = new Lazy<ChannelCredentials?>(() => googleServices.GetChannelCredentials(
                SubscriberServiceApiClient.DefaultEndpoint,
                SubscriberServiceApiClient.DefaultScopes));
            _subscriberServiceEndpoint = new Lazy<string?>(() => googleServices.GetServiceEndpoint(
                SubscriberServiceApiClient.DefaultEndpoint,
                SubscriberServiceApiClient.DefaultScopes));

            _subscriptions = new Dictionary<string, SubscriptionState>();

            OnSecretUpdated = new List<Func<Secret, CancellationToken, Task<SubscriberClient.Reply>>>();

            // Figure out the subscription suffix, which is used to allow multiple pods in Kubernetes to be subscribed at the same time.
            var suffixProvider = serviceProvider.GetService<ISecretManagerNotificationSuffixProvider>();
            if (suffixProvider != null)
            {
                _subscriptionSuffix = "-" + suffixProvider.Suffix;
            }
            else
            {
                var subscriptionSuffix = Environment.GetEnvironmentVariable("SECRET_MANAGER_SUBSCRIPTION_SUFFIX");
                if (!string.IsNullOrWhiteSpace(subscriptionSuffix))
                {
                    _subscriptionSuffix = "-" + subscriptionSuffix;
                }
                else
                {
                    _subscriptionSuffix = null;
                }
            }
        }

        public List<Func<Secret, CancellationToken, Task<SubscriberClient.Reply>>> OnSecretUpdated { get; private init; }

        private SubscriptionState? SubscribeInternal(Secret secret)
        {
            if (_subscriptionSuffix == null)
            {
                _logger.LogError("Expected 'SECRET_MANAGER_SUBSCRIPTION_SUFFIX' environment variable to be set so we can create a unique subscription per process. This should usually be set to the value of `metadata.podName` if you're running the application in Kubernetes. Refer to https://kubernetes.io/docs/tasks/inject-data-application/environment-variable-expose-pod-information/#use-pod-fields-as-values-for-environment-variables for more information.");
                return null;
            }

            var secretName = secret.SecretName.SecretId;

            if (!_subscriptions.TryGetValue(secretName, out SubscriptionState? value))
            {
                value = new SubscriptionState(
                    secret,
                    SubscriptionName.Format(_googleServices.ProjectId, $"{secretName}-notifications-update{_subscriptionSuffix}"));
                _subscriptions.Add(secretName, value);
            }
            if (value.SubscriberInitTask != null)
            {
                // We've already been subscribed.
                return null;
            }

            var notificationTopic = secret.Topics.FirstOrDefault(x => x.TopicName.TopicId == $"{secretName}-notifications");
            if (notificationTopic == null)
            {
                _logger.LogError($"Expected '{secretName}' secret to have an '{secretName}-notifications' topic that we can subscribe to for update notifications. Since one doesn't exist, the application will not refresh configuration when the secrets are updated inside Google Cloud Secret Manager.");
                return null;
            }

            value.SubscriberInitTask = Task.Run(async () => await StartSubscriberAsync(secretName, value, notificationTopic).ConfigureAwait(false));
            return value;
        }

        public void Subscribe(Secret secret)
        {
            SubscribeInternal(secret);
        }

        public async Task SubscribeAsync(Secret secret)
        {
            var value = SubscribeInternal(secret);
            if (value?.SubscriberInitTask == null)
            {
                return;
            }
            await value.SubscriberInitTask.ConfigureAwait(false);
        }

        public async Task UnsubscribeAsync(Secret secret)
        {
            var secretName = secret.SecretName.SecretId;

            if (!_subscriptions.TryGetValue(secretName, out SubscriptionState? value))
            {
                // Not subscribed.
                return;
            }

            if (value.SubscriptionCount > 0)
            {
                value.SubscriptionCount -= 1;
            }

            if (value.SubscriptionCount == 0)
            {
                // Cancel and shutdown background task.
                value.CancellationTokenSource.Cancel();
                try
                {
                    if (value.SubscriberInitTask != null)
                    {
                        await value.SubscriberInitTask.ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                if (value.Subscriber != null)
                {
                    try
                    {
                        await value.Subscriber.StopAsync(value.CancellationTokenSource.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
                try
                {
                    if (value.SubscriberRunTask != null)
                    {
                        await value.SubscriberRunTask.ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                value.CancellationTokenSource.Dispose();
                value.CancellationTokenSource = new CancellationTokenSource();
                value.Subscriber = null;
                value.SubscriberInitTask = null;
                value.SubscriberRunTask = null;
            }
        }

        private async Task StartSubscriberAsync(string secretName, SubscriptionState value, Topic notificationTopic)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    Subscription? subscription = null;
                    try
                    {
                        subscription = _subscriberClient.Value.GetSubscription(value.SubscriptionName);
                    }
                    catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
                    {
                    }
                    if (subscription == null)
                    {
                        subscription = _subscriberClient.Value.CreateSubscription(new Subscription
                        {
                            Name = value.SubscriptionName,
                            AckDeadlineSeconds = 60,
                            DeadLetterPolicy = null,
                            Detached = false,
                            MessageRetentionDuration = Duration.FromTimeSpan(TimeSpan.FromMinutes(20)),
                            Filter = "hasPrefix(attributes.eventType, \"SECRET_VERSION_\")",
                            EnableMessageOrdering = false,
                            ExpirationPolicy = new ExpirationPolicy
                            {
                                Ttl = Duration.FromTimeSpan(TimeSpan.FromDays(1)),
                            },
                            Labels =
                            {
                                { "auto-managed-by", "redpoint-cloudframework" }
                            },
                            PushConfig = null,
                            RetainAckedMessages = false,
                            Topic = notificationTopic.Name,
                        });
                    }

                    var builder = new SubscriberClientBuilder
                    {
                        SubscriptionName = subscription.SubscriptionName,
                        ClientCount = 1,
                        ApiSettings = null,
                        ChannelCredentials = _subscriberCredential.Value,
                        Endpoint = _subscriberServiceEndpoint.Value,
                        Settings = new SubscriberClient.Settings
                        {
                            FlowControlSettings = new FlowControlSettings(1, null)
                        }
                    };
                    value.Subscriber = builder.Build();
                    value.SubscriberRunTask = value.Subscriber.StartAsync(async (message, cancellationToken) =>
                    {
                        var handled = SubscriberClient.Reply.Nack;
                        foreach (var handler in OnSecretUpdated)
                        {
                            var handlerHandled = await handler(value.Secret, cancellationToken).ConfigureAwait(false);
                            if (handlerHandled == SubscriberClient.Reply.Ack)
                            {
                                handled = SubscriberClient.Reply.Ack;
                            }
                        }
                        return handled;
                    });
                    _logger.LogInformation($"Subscribed to '{secretName}' update notifications via Pub/Sub from Google Cloud Secret Manager.");
                    return;
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
                {
                    if (i == 9)
                    {
                        _logger.LogWarning($"Got 'already exists' while trying to subscribe to the '{value.SubscriptionName}' subscription. This means we couldn't get it through GetSubscription, but also can't create it with CreateSubscription. Something is very weird.");
                    }
                    else
                    {
                        await Task.Delay(i * 1000).ConfigureAwait(false);
                        continue;
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
                {
                    if (i == 9)
                    {
                        _logger.LogWarning($"Got 'not found' while trying to subscribe to the '{value.SubscriptionName}' subscription, even though we should have created it during startup.");
                    }
                    else
                    {
                        await Task.Delay(i * 1000).ConfigureAwait(false);
                        continue;
                    }
                }
            }
        }

        public async ValueTask UnsubscribeAllAsync()
        {
            if (_subscriptions.Count == 0)
            {
                return;
            }

            // Force each subscription to unsubscribe.
            foreach (var subscription in _subscriptions)
            {
                subscription.Value.SubscriptionCount = 1;
                await UnsubscribeAsync(subscription.Value.Secret).ConfigureAwait(false);
            }

            // Go through each subscription entry and delete the underlying Pub/Sub subscription if it exists.
            foreach (var subscription in _subscriptions)
            {
                try
                {
                    var pubsubSubscription = await _subscriberClient.Value.GetSubscriptionAsync(
                        subscription.Value.SubscriptionName).ConfigureAwait(false);
                    if (pubsubSubscription != null)
                    {
                        _logger.LogInformation($"Cleaning up '{subscription.Key}' notification subscription from Google Pub/Sub.");
                        await _subscriberClient.Value.DeleteSubscriptionAsync(subscription.Value.SubscriptionName).ConfigureAwait(false);
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
                {
                    _logger.LogInformation($"No '{subscription.Key}' notification subscription was found Google Pub/Sub, so it was not cleaned up.");
                }
            }

            // Dispose cancellation token sources.
            var keys = _subscriptions.Keys.ToList();
            foreach (var key in keys)
            {
                _subscriptions[key].Dispose();
                _subscriptions.Remove(key);
            }
        }

        public ValueTask DisposeAsync()
        {
            return UnsubscribeAllAsync();
        }
    }
}
