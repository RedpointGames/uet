namespace Redpoint.Uefs.ContainerRegistry
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents the content of a ~/.docker/config.json file.
    /// </summary>
    public class DockerConfigJson
    {
        /// <summary>
        /// The saved authentication credentials for each host.
        /// </summary>
        [JsonPropertyName("auths")]
        public Dictionary<string, DockerAuthSetting> Auths { get; } = new Dictionary<string, DockerAuthSetting>();

        /// <summary>
        /// The type of credential store being used.
        /// </summary>
        [JsonPropertyName("credsStore")]
        public string CredsStore { get; set; } = string.Empty;
    }
}
