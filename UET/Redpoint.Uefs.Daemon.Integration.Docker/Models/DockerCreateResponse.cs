namespace Redpoint.Uefs.Daemon.Integration.Docker.Models
{
    using System.Text.Json.Serialization;

    public class DockerCreateResponse
    {
        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
