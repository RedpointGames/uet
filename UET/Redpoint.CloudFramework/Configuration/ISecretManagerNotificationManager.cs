namespace Redpoint.CloudFramework.Configuration
{
    using Google.Cloud.PubSub.V1;
    using Google.Cloud.SecretManager.V1;
    using System;

    internal interface ISecretManagerNotificationManager : IAsyncDisposable
    {
        void Subscribe(Secret secret);

        Task SubscribeAsync(Secret secret);

        Task UnsubscribeAsync(Secret secret);

        List<Func<Secret, CancellationToken, Task<SubscriberClient.Reply>>> OnSecretUpdated { get; }

        ValueTask UnsubscribeAllAsync();
    }
}
