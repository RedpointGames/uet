namespace Redpoint.Uefs.Daemon.Integration.Docker.Models
{
    using System.Text.Json.Serialization;

    public class DockerCreateRequest
    {
        [JsonPropertyName("Name")]
        public string Name = string.Empty;

        [JsonPropertyName("Opts")]
        public Dictionary<string, string> Opts = new Dictionary<string, string>();
    }
}
