namespace Redpoint.CloudFramework.Configuration
{
    using Google.Cloud.SecretManager.V1;
    using Microsoft.Extensions.Logging;

    internal class DefaultAutoRefreshingSecretFactory : IAutoRefreshingSecretFactory
    {
        private readonly ILogger<DefaultAutoRefreshingSecretFactory> _logger;
        private readonly ISecretManagerAccess _secretManagerAccess;
        private readonly ISecretManagerNotificationManager _secretManagerNotificationManager;

        public DefaultAutoRefreshingSecretFactory(
            ILogger<DefaultAutoRefreshingSecretFactory> logger,
            ISecretManagerAccess secretManagerAccess,
            ISecretManagerNotificationManager secretManagerNotificationManager)
        {
            _logger = logger;
            _secretManagerAccess = secretManagerAccess;
            _secretManagerNotificationManager = secretManagerNotificationManager;
        }

        public IAutoRefreshingSecret Create(string secretName, bool requireSuccessfulLoad)
        {
            var secret = _secretManagerAccess.TryGetSecret(secretName);
            if (secret == null)
            {
                if (requireSuccessfulLoad)
                {
                    throw new SecretManagerSecretFailedToLoadException($"The '{secretName}' secret could be found in Google Cloud Secret Manager.");
                }
                else
                {
                    _logger.LogWarning($"No '{secretName}' secret could be found in Google Cloud Secret Manager; returning an empty secret.");
                    return new EmptyAutoRefreshingSecret();
                }
            }
            else
            {
                _logger.LogInformation($"Successfully loaded '{secretName}' secret from Google Cloud Secret Manager.");
            }

            var secretVersion = _secretManagerAccess.TryGetLatestSecretVersion(secret);
            if (secretVersion == null)
            {
                if (requireSuccessfulLoad)
                {
                    throw new SecretManagerSecretFailedToLoadException($"No enabled version of the '{secretName}' secret could be found in Google Cloud Secret Manager.");
                }
                else
                {
                    _logger.LogWarning($"No enabled '{secretName}' secret could be found in Google Cloud Secret Manager; the secret will initially have no data but can be populated by creating a new secret version in the Google Cloud dashboard.");
                }
            }
            else
            {
                _logger.LogInformation($"Successfully determined the latest version '{secretVersion.SecretVersionName.SecretVersionId}' of the '{secretName}' secret from Google Cloud Secret Manager.");
            }

            AccessSecretVersionResponse? accessedSecretVersion;
            if (secretVersion != null)
            {
                accessedSecretVersion = _secretManagerAccess.TryAccessSecretVersion(secretVersion);
                if (accessedSecretVersion == null)
                {
                    if (requireSuccessfulLoad)
                    {
                        throw new SecretManagerSecretFailedToLoadException($"Unable to access the latest enabled version of the '{secretName}' secret in Google Cloud Secret Manager.");
                    }
                    else
                    {
                        _logger.LogWarning($"Unable to access the latest enabled version of the '{secretName}' secret in Google Cloud Secret Manager; the secret will initially have no data but can be populated by creating a new accessible secret version in the Google Cloud dashboard.");
                    }
                }
                else
                {
                    _logger.LogInformation($"Successfully loaded the latest version '{secretVersion.SecretVersionName.SecretVersionId}' of the '{secretName}' secret from Google Cloud Secret Manager.");
                }
            }
            else
            {
                accessedSecretVersion = null;
            }

            return new DefaultAutoRefreshingSecret(
                _logger,
                _secretManagerAccess,
                _secretManagerNotificationManager,
                secret,
                accessedSecretVersion);
        }
    }
}
