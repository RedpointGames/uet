namespace Redpoint.CredentialDiscovery
{
    using System.Text.Json.Serialization;

    internal sealed class DockerConfigJson
    {
        internal sealed class DockerAuthSetting
        {
            [JsonPropertyName("auth")]
            public string Auth { get; set; } = string.Empty;
        }

        [JsonPropertyName("auths")]
        public Dictionary<string, DockerAuthSetting> Auths { get; set; } = new Dictionary<string, DockerAuthSetting>();

        [JsonPropertyName("credsStore")]
        public string CredsStore { get; set; } = string.Empty;
    }
}