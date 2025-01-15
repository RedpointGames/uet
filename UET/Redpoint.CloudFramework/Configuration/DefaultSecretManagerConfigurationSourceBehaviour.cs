namespace Redpoint.CloudFramework.Configuration
{
    internal class DefaultSecretManagerConfigurationSourceBehaviour : ISecretManagerConfigurationSourceBehaviour
    {
        private readonly string _secretName;
        private readonly bool _requireSuccessfulLoad;

        public DefaultSecretManagerConfigurationSourceBehaviour(string secretName, bool requireSuccessfulLoad)
        {
            _secretName = secretName;
            _requireSuccessfulLoad = requireSuccessfulLoad;
        }

        public string SecretName => _secretName;

        public bool RequireSuccessfulLoad => _requireSuccessfulLoad;
    }
}
