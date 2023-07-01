namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    using System.Text.Json.Serialization;

    public class UEFSGitFetchRequest
    {
        [JsonPropertyName("GitUrl")]
        public string? GitUrl = null;

        [JsonPropertyName("GitCommit")]
        public string? GitCommit = null;

        [JsonPropertyName("GitPublicKeyString")]
        public string? GitPublicKeyString = null;

        [JsonPropertyName("GitPrivateKeyString")]
        public string? GitPrivateKeyString = null;
    }
}
