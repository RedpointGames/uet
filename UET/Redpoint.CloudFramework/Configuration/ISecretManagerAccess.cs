namespace Redpoint.CloudFramework.Configuration
{
    using Google.Cloud.SecretManager.V1;

    internal interface ISecretManagerAccess
    {
        SecretManagerServiceClient SecretClient { get; }

        Secret? TryGetSecret(string secretName);
        SecretVersion? TryGetLatestSecretVersion(Secret secret);
        AccessSecretVersionResponse? TryAccessSecretVersion(SecretVersion secretVersion);

        Task<SecretVersion?> TryGetLatestSecretVersionAsync(Secret secret);
        Task<AccessSecretVersionResponse?> TryAccessSecretVersionAsync(SecretVersion secretVersion);
    }
}
