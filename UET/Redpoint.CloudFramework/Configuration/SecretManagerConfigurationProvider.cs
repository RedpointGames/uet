namespace Redpoint.CloudFramework.Configuration
{
    using Microsoft.Extensions.Configuration;

    internal class SecretManagerConfigurationProvider : ConfigurationProvider
    {
        private readonly IAutoRefreshingSecretFactory _autoRefreshingSecretFactory;
        private readonly ISecretManagerConfigurationSourceBehaviour _secretManagerConfigurationSourceBehaviour;
        internal IAutoRefreshingSecret? _autoRefreshingSecret;

        public SecretManagerConfigurationProvider(
            IAutoRefreshingSecretFactory autoRefreshingSecretFactory,
            ISecretManagerConfigurationSourceBehaviour secretManagerConfigurationSourceBehaviour)
        {
            _autoRefreshingSecretFactory = autoRefreshingSecretFactory;
            _secretManagerConfigurationSourceBehaviour = secretManagerConfigurationSourceBehaviour;
            _autoRefreshingSecret = null;
        }

        public override void Load()
        {
            _autoRefreshingSecret = _autoRefreshingSecretFactory.Create(
                _secretManagerConfigurationSourceBehaviour.SecretName,
                _secretManagerConfigurationSourceBehaviour.RequireSuccessfulLoad);
            _autoRefreshingSecret.OnRefreshed = () =>
            {
                Data = _autoRefreshingSecret.Data;
                OnReload();
            };
            Data = _autoRefreshingSecret.Data;
        }
    }
}
