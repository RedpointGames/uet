namespace Redpoint.Uefs.Daemon.Integration.Docker.Models
{
    using System.Text.Json.Serialization;

    public class DockerCapabilitiesResponse
    {
        [JsonPropertyName("Capabilities")]
        public DockerCapabilitiesInfo Capabilities = new DockerCapabilitiesInfo();
    }
}
