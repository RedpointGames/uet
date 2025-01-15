namespace Redpoint.CloudFramework.Configuration
{
    using Google.Cloud.PubSub.V1;
    using Google.Cloud.SecretManager.V1;
    using Microsoft.Extensions.Logging;

    internal class DefaultAutoRefreshingSecret : IAutoRefreshingSecret
    {
        private readonly ILogger _logger;
        private readonly ISecretManagerAccess _secretManagerAccess;
        private readonly ISecretManagerNotificationManager _secretManagerNotificationManager;
        private readonly Secret _secret;
        private readonly Func<Secret, CancellationToken, Task<SubscriberClient.Reply>> _notifier;

        public DefaultAutoRefreshingSecret(
            ILogger logger,
            ISecretManagerAccess secretManagerAccess,
            ISecretManagerNotificationManager secretManagerNotificationManager,
            Secret secret,
            AccessSecretVersionResponse? initialAccessedSecretVersion)
        {
            _logger = logger;
            _secretManagerAccess = secretManagerAccess;
            _secretManagerNotificationManager = secretManagerNotificationManager;
            _secret = secret;
            _notifier = OnSecretUpdated;

            _secretManagerNotificationManager.OnSecretUpdated.Add(_notifier);
            _secretManagerNotificationManager.Subscribe(secret);

            if (initialAccessedSecretVersion == null)
            {
                Data = new Dictionary<string, string?>();
            }
            else
            {
                using (var stream = new MemoryStream(initialAccessedSecretVersion.Payload.Data.ToByteArray()))
                {
                    Data = JsonConfigurationParser.Parse(stream);
                }
            }
        }

        public IDictionary<string, string?> Data { get; private set; }

        public Action? OnRefreshed { get; set; }

        private async Task<SubscriberClient.Reply> OnSecretUpdated(Secret updatedSecret, CancellationToken cancellationToken)
        {
            if (updatedSecret.SecretName.SecretId != _secret.SecretName.SecretId)
            {
                return SubscriberClient.Reply.Nack;
            }

            var secretVersion = await _secretManagerAccess.TryGetLatestSecretVersionAsync(_secret).ConfigureAwait(false);
            if (secretVersion == null)
            {
                return SubscriberClient.Reply.Nack;
            }

            var accessedSecretVersion = await _secretManagerAccess.TryAccessSecretVersionAsync(secretVersion).ConfigureAwait(false);
            if (accessedSecretVersion == null)
            {
                return SubscriberClient.Reply.Nack;
            }

            using (var stream = new MemoryStream(accessedSecretVersion.Payload.Data.ToByteArray()))
            {
                Data = JsonConfigurationParser.Parse(stream);
            }
            if (OnRefreshed != null)
            {
                OnRefreshed();
            }

            _logger.LogInformation($"Refreshed '{_secret.SecretName.SecretId}' secret from Google Cloud Secret Manager, using version '{secretVersion.SecretVersionName.SecretVersionId}'.");

            return SubscriberClient.Reply.Ack;
        }

        public async ValueTask DisposeAsync()
        {
            _secretManagerNotificationManager.OnSecretUpdated.Remove(_notifier);
            await _secretManagerNotificationManager.UnsubscribeAsync(_secret).ConfigureAwait(false);
        }
    }
}
