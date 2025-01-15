namespace Redpoint.CloudFramework.Configuration
{
    internal interface ISecretManagerConfigurationSourceBehaviour
    {
        string SecretName { get; }

        bool RequireSuccessfulLoad { get; }
    }
}
