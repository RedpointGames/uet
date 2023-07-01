namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    using Redpoint.Uefs.ContainerRegistry;
    using System.Text.Json.Serialization;

    public class UEFSPullRequest
    {
        [JsonPropertyName("Url")]
        public string Url = string.Empty;

        [JsonPropertyName("Credential")]
        public RegistryCredential Credential = new RegistryCredential();
    }
}
