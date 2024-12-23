namespace UET.Commands.Internal.RemoteZfsServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    internal class RemoteZfsServerConfig
    {
        /// <summary>
        /// The port that the remote ZFS server will listen on. If not set, defaults to 9000.
        /// </summary>
        [JsonPropertyName("ServerPort")]
        public int? ServerPort { get; set; }

        /// <summary>
        /// The TrueNAS REST API key to use.
        /// </summary>
        [JsonPropertyName("TrueNasApiKey")]
        public required string TrueNasApiKey { get; set; }

        /// <summary>
        /// The URL of the TrueNAS REST API.
        /// </summary>
        [JsonPropertyName("TrueNasUrl")]
        public required string TrueNasUrl { get; set; }

        /// <summary>
        /// The available templates.
        /// </summary>
        [JsonPropertyName("Templates")]
        public required RemoteZfsServerConfigTemplate[] Templates { get; set; }
    }
}
