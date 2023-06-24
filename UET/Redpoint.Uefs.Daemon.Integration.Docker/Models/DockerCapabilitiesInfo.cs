namespace Redpoint.Uefs.Daemon.Integration.Docker.Models
{
    using System.Text.Json.Serialization;

    public class DockerCapabilitiesInfo
    {
        [JsonPropertyName("Scope")]
        public string Scope = "local";
    }
}
