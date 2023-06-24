namespace Redpoint.Uefs.Daemon.Integration.Docker.Models
{
    using System.Text.Json.Serialization;

    public class DockerHandshakeResponse
    {
        [JsonPropertyName("Implements")]
        public string[] Implements = new string[0];
    }
}
