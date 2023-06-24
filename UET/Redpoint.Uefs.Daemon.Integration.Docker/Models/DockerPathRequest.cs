namespace Redpoint.Uefs.Daemon.Integration.Docker.Models
{
    using System.Text.Json.Serialization;

    public class DockerPathRequest
    {
        [JsonPropertyName("Name")]
        public string Name = string.Empty;
    }
}
