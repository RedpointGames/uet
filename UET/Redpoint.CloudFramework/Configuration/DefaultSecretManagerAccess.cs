namespace Redpoint.CloudFramework.Configuration
{
    using Google.Cloud.SecretManager.V1;
    using Grpc.Core;
    using Redpoint.CloudFramework.GoogleInfrastructure;
    using System;
    using System.Linq;

    internal class DefaultSecretManagerAccess : ISecretManagerAccess
    {
        private readonly IGoogleServices _googleServices;
        private readonly Lazy<SecretManagerServiceClient> _secretClient;

        public DefaultSecretManagerAccess(
            IGoogleServices googleServices)
        {
            _googleServices = googleServices;
            _secretClient = new Lazy<SecretManagerServiceClient>(() => googleServices.Build<SecretManagerServiceClient, SecretManagerServiceClientBuilder>(
                SecretManagerServiceClient.DefaultEndpoint,
                SecretManagerServiceClient.DefaultScopes));
        }

        public SecretManagerServiceClient SecretClient => _secretClient.Value;

        public Secret? TryGetSecret(string secretName)
        {
            Secret? secret = null;
            try
            {
                secret = _secretClient.Value.GetSecret(SecretName.Format(_googleServices.ProjectId, secretName));
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
            }
            return secret;
        }

        public SecretVersion? TryGetLatestSecretVersion(Secret secret)
        {
            var versions = _secretClient.Value.ListSecretVersions(new ListSecretVersionsRequest
            {
                Filter = "state:(ENABLED)",
                PageSize = 1,
                ParentAsSecretName = secret.SecretName,
            });

            var availableVersion = versions.FirstOrDefault();

            return availableVersion;
        }

        public AccessSecretVersionResponse? TryAccessSecretVersion(SecretVersion secretVersion)
        {
            AccessSecretVersionResponse? response = null;
            try
            {
                response = _secretClient.Value.AccessSecretVersion(secretVersion.SecretVersionName);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
            }
            return response;
        }

        public async Task<SecretVersion?> TryGetLatestSecretVersionAsync(Secret secret)
        {
            await foreach (var availableVersion in _secretClient.Value.ListSecretVersionsAsync(new ListSecretVersionsRequest
            {
                Filter = "state:(ENABLED)",
                PageSize = 1,
                ParentAsSecretName = secret.SecretName,
            }).ConfigureAwait(false))
            {
                return availableVersion;
            }

            return null;
        }

        public async Task<AccessSecretVersionResponse?> TryAccessSecretVersionAsync(SecretVersion secretVersion)
        {
            AccessSecretVersionResponse? response = null;
            try
            {
                response = await _secretClient.Value.AccessSecretVersionAsync(secretVersion.SecretVersionName).ConfigureAwait(false);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
            }
            return response;
        }
    }
}
