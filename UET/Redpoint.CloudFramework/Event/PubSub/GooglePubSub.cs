namespace Redpoint.CloudFramework.Event.PubSub
{
    using Google.Api.Gax;
    using Google.Cloud.PubSub.V1;
    using Google.Protobuf;
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Redpoint.CloudFramework.GoogleInfrastructure;
    using Redpoint.CloudFramework.Metric;
    using Redpoint.CloudFramework.Prefix;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class GooglePubSub : IPubSub, IDisposable
    {
        private readonly ILogger<GooglePubSub> _logger;
        private readonly IGoogleServices _googleServices;
        private readonly IGoogleApiRetry _googleApiRetry;
        private readonly IMetricService _metricService;
        private readonly IGlobalPrefix _globalPrefix;

        private readonly PublisherServiceApiClient _publisherClient;
        private readonly SubscriberServiceApiClient _subscriberClient;
        private readonly ChannelCredentials? _publisherCredential;
        private readonly string? _publisherServiceEndpoint;
        private readonly ChannelCredentials? _subscriberCredential;
        private readonly string? _subscriberServiceEndpoint;
        private Dictionary<string, PublisherClient> _publisherClients;
        private readonly SemaphoreSlim _publisherClientCreation;

        private const string _googlePubSubPushCount = "rcf/pubsub_push_count";
        private const string _googlePubSubPullCount = "rcf/pubsub_pull_count";
        private const string _googlePubSubAckCount = "rcf/pubsub_ack_count";
        private const string _googlePubSubNackCount = "rcf/pubsub_nack_count";
        private const string _googlePubSubNackFailCount = "rcf/pubsub_nack_fail_count";

        public GooglePubSub(
            ILogger<GooglePubSub> logger,
            IMetricService metricService,
            IGoogleServices googleServices,
            IGoogleApiRetry googleApiRetry,
            IGlobalPrefix globalPrefix)
        {
            _logger = logger;
            _googleServices = googleServices;
            _googleApiRetry = googleApiRetry;
            _metricService = metricService;
            _globalPrefix = globalPrefix;

            _publisherClient = _googleServices.Build<PublisherServiceApiClient, PublisherServiceApiClientBuilder>(
                PublisherServiceApiClient.DefaultEndpoint,
                PublisherServiceApiClient.DefaultScopes);
            _subscriberClient = _googleServices.Build<SubscriberServiceApiClient, SubscriberServiceApiClientBuilder>(
                SubscriberServiceApiClient.DefaultEndpoint,
                SubscriberServiceApiClient.DefaultScopes);
            _publisherCredential = _googleServices.GetChannelCredentials(
                PublisherServiceApiClient.DefaultEndpoint,
                PublisherServiceApiClient.DefaultScopes);
            _publisherServiceEndpoint = _googleServices.GetServiceEndpoint(
                PublisherServiceApiClient.DefaultEndpoint,
                PublisherServiceApiClient.DefaultScopes);
            _subscriberCredential = _googleServices.GetChannelCredentials(
                SubscriberServiceApiClient.DefaultEndpoint,
                SubscriberServiceApiClient.DefaultScopes);
            _subscriberServiceEndpoint = _googleServices.GetServiceEndpoint(
                SubscriberServiceApiClient.DefaultEndpoint,
                SubscriberServiceApiClient.DefaultScopes);
            _publisherClients = new Dictionary<string, PublisherClient>();
            _publisherClientCreation = new SemaphoreSlim(1);
        }

        public async Task PublishAsync(SerializedEvent jsonObject)
        {
            ArgumentNullException.ThrowIfNull(jsonObject);

            var topicRawName = "event~" + jsonObject.Type?.Replace(':', '.');
            var topicName = new TopicName(
                _googleServices.ProjectId,
                topicRawName);

            PublisherClient client;
            await _publisherClientCreation.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_publisherClients.TryGetValue(topicRawName, out client!))
                {
                    try
                    {
                        // Attempt to create the topic in case it doesn't exist.
                        await _publisherClient.CreateTopicAsync(topicName).ConfigureAwait(false);
                    }
                    catch (RpcException ex2) when (ex2.Status.StatusCode == StatusCode.AlreadyExists)
                    {
                        // Already exists.
                    }

                    var builder = new PublisherClientBuilder
                    {
                        TopicName = topicName,
                        ClientCount = 1,
                        ApiSettings = null,
                        ChannelCredentials = _publisherCredential,
                        Endpoint = _publisherServiceEndpoint,
                    };
                    client = await builder.BuildAsync().ConfigureAwait(false);
                    _publisherClients[topicRawName] = client;
                }
            }
            finally
            {
                _publisherClientCreation.Release();
            }

            await client.PublishAsync(new PubsubMessage
            {
                Data = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(jsonObject))
            }).ConfigureAwait(false);

            await _metricService.AddPoint(
                _googlePubSubPushCount,
                1,
                jsonObject.Project == null ? null : _globalPrefix.ParseInternal(string.Empty, jsonObject.Project),
                new Dictionary<string, string?>
                {
                    { "event_type", jsonObject.Type },
                    { "entity_type", jsonObject.Key == null ? "(no entity in event)" : _globalPrefix.ParseInternal(string.Empty, jsonObject.Key).Path.Last().Kind },
                }).ConfigureAwait(false);
        }

        public async Task SubscribeAndLoopUntilCancelled(
            string subscriptionName,
            string[] eventTypes,
            SubscriptionCleanupPolicy cleanupPolicy,
            Func<SerializedEvent, Task<bool>> onMessage,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(eventTypes);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // Subscribe to all of the topics for all of the event types in parallel.
            using var derivedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var derivedCancellationToken = derivedCancellationTokenSource.Token;
            var tasks = new Task[eventTypes.Length];
            for (var i = 0; i < eventTypes.Length; i++)
            {
                var eventType = eventTypes[i];

                var topicName = new TopicName(
                    _googleServices.ProjectId,
                    "event~" + eventType.Replace(':', '.'));
                var subscriberName = new SubscriptionName(
                    _googleServices.ProjectId,
                    subscriptionName + "~" + eventType.Replace(':', '.'));

                try
                {
                    await _googleApiRetry.DoRetryableOperationAsync(GoogleApiCallContext.PubSub, _logger, async () =>
                    {
                        await _subscriberClient.CreateSubscriptionAsync(
                            subscriberName,
                            topicName,
                            null,
                            600,
                            derivedCancellationToken).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                }
                catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.NotFound)
                {
                    try
                    {
                        // The topic wasn't found, so create it.
                        await _googleApiRetry.DoRetryableOperationAsync(GoogleApiCallContext.PubSub, _logger, async () =>
                        {
                            await _publisherClient.CreateTopicAsync(topicName, derivedCancellationToken).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }
                    catch (RpcException ex2) when (ex2.Status.StatusCode == StatusCode.AlreadyExists)
                    {
                        // Topic has been created in parallel.
                    }

                    try
                    {
                        // Now create the subscription.
                        await _googleApiRetry.DoRetryableOperationAsync(GoogleApiCallContext.PubSub, _logger, async () =>
                        {
                            await _subscriberClient.CreateSubscriptionAsync(
                            subscriberName,
                            topicName,
                            null,
                            600,
                            derivedCancellationToken).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }
                    catch (RpcException ex2) when (ex2.Status.StatusCode == StatusCode.AlreadyExists)
                    {
                        // Subscription already exists; everything is OK.
                    }
                }
                catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.AlreadyExists)
                {
                    // Subscription already exists; everything is OK.
                }

                // Set up the task to continously poll.
                tasks[i] = Task.Run(async () =>
                {
                    var builder = new SubscriberClientBuilder
                    {
                        SubscriptionName = subscriberName,
                        ClientCount = 1,
                        ApiSettings = null,
                        ChannelCredentials = _subscriberCredential,
                        Endpoint = _subscriberServiceEndpoint,
                        Settings = new SubscriberClient.Settings
                        {
                            FlowControlSettings = new FlowControlSettings(1, null)
                        }
                    };
                    var simpleSubscriber = await builder.BuildAsync().ConfigureAwait(false);
                    derivedCancellationToken.Register(() =>
                    {
                        // Can't return a task here?
                        simpleSubscriber.StopAsync(TimeSpan.FromMinutes(3));
                    });
                    await simpleSubscriber.StartAsync(async (message, cancellationTokenInner) =>
                    {
                        SerializedEvent? serializedEvent = null;
                        try
                        {
                            serializedEvent = JsonConvert.DeserializeObject<SerializedEvent>(
                                message.Data.ToStringUtf8())!;
                            _logger.LogInformation($"Recieved event {serializedEvent.Id} from Google Pub/Sub for {eventType} events from {subscriptionName}");
                            await _metricService.AddPoint(
                                _googlePubSubPullCount,
                                1,
                                serializedEvent.Project == null ? null : _globalPrefix.ParseInternal(string.Empty, serializedEvent.Project),
                                new Dictionary<string, string?>
                                {
                                    { "event_type", serializedEvent.Type },
                                    { "subscription_name", subscriptionName },
                                    { "entity_type", serializedEvent.Key == null ? "(no entity in event)" : _globalPrefix.ParseInternal(string.Empty, serializedEvent.Key).Path.Last().Kind },
                                }).ConfigureAwait(false);
                            if (await onMessage(serializedEvent).ConfigureAwait(false))
                            {
                                await _metricService.AddPoint(
                                    _googlePubSubAckCount,
                                    1,
                                    serializedEvent.Project == null ? null : _globalPrefix.ParseInternal(string.Empty, serializedEvent.Project),
                                    new Dictionary<string, string?>
                                    {
                                        { "event_type", serializedEvent.Type },
                                        { "subscription_name", subscriptionName },
                                        { "entity_type", serializedEvent.Key == null ? "(no entity in event)" : _globalPrefix.ParseInternal(string.Empty, serializedEvent.Key).Path.Last().Kind },
                                    }).ConfigureAwait(false);
                                return SubscriberClient.Reply.Ack;
                            }
                            else
                            {
                                await _metricService.AddPoint(
                                    _googlePubSubNackCount,
                                    1,
                                    serializedEvent.Project == null ? null : _globalPrefix.ParseInternal(string.Empty, serializedEvent.Project),
                                    new Dictionary<string, string?>
                                    {
                                        { "event_type", serializedEvent.Type },
                                        { "subscription_name", subscriptionName },
                                        { "entity_type", serializedEvent.Key == null ? "(no entity in event)" : _globalPrefix.ParseInternal(string.Empty, serializedEvent.Key).Path.Last().Kind },
                                    }).ConfigureAwait(false);
                                return SubscriberClient.Reply.Nack;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, ex.Message);

                            if (serializedEvent != null)
                            {
                                await _metricService.AddPoint(
                                    _googlePubSubNackFailCount,
                                    1,
                                    serializedEvent.Project == null ? null : _globalPrefix.ParseInternal(string.Empty, serializedEvent.Project),
                                    new Dictionary<string, string?>
                                    {
                                        { "event_type", serializedEvent.Type },
                                        { "subscription_name", subscriptionName },
                                        { "entity_type", serializedEvent.Key == null ? "(no entity in event)" : _globalPrefix.ParseInternal(string.Empty, serializedEvent.Key).Path.Last().Kind },
                                    }).ConfigureAwait(false);
                            }
                            return SubscriberClient.Reply.Nack;
                        }
                    }).ConfigureAwait(false);
                }, derivedCancellationToken);
            }

            await Task.WhenAny(tasks).ConfigureAwait(false);

            derivedCancellationTokenSource.Cancel();

            if (cleanupPolicy == SubscriptionCleanupPolicy.DeleteSubscription)
            {
                for (var i = 0; i < eventTypes.Length; i++)
                {
                    var eventType = eventTypes[i];

                    var subscriberName = new SubscriptionName(
                        _googleServices.ProjectId,
                        subscriptionName + "~" + eventType.Replace(':', '.'));

                    await _googleApiRetry.DoRetryableOperationAsync(GoogleApiCallContext.PubSub, _logger, async () =>
                    {
                        await _subscriberClient.DeleteSubscriptionAsync(subscriberName).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                }
            }
        }

        public void Dispose()
        {
            ((IDisposable)_publisherClientCreation).Dispose();
        }
    }
}
