using System.Text.Json.Serialization;

namespace Redpoint.Uefs.ContainerRegistry
{
    /// <summary>
    /// Represents the content of a ~/.docker/config.json file.
    /// </summary>
    public class DockerConfigJson
    {
        /// <summary>
        /// Represents an authentication entry inside a Docker configuration file.
        /// </summary>
        public class DockerAuthSetting
        {
            /// <summary>
            /// The authentication information.
            /// </summary>
            [JsonPropertyName("auth")]
            public string Auth = string.Empty;
        }

        /// <summary>
        /// The saved authentication credentials for each host.
        /// </summary>
        [JsonPropertyName("auths")]
        public Dictionary<string, DockerAuthSetting> Auths = new Dictionary<string, DockerAuthSetting>();

        /// <summary>
        /// The type of credential store being used.
        /// </summary>
        [JsonPropertyName("credsStore")]
        public string CredsStore = string.Empty;
    }
}
