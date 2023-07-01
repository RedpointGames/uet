namespace Redpoint.Uefs.ContainerRegistry
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents an authentication entry inside a Docker configuration file.
    /// </summary>
    public class DockerAuthSetting
    {
        /// <summary>
        /// The authentication information.
        /// </summary>
        [JsonPropertyName("auth")]
        public string Auth { get; set; } = string.Empty;
    }
}
