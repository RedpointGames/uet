namespace Redpoint.Uefs.Daemon.Integration.Docker.Models
{
    using System.Text.Json.Serialization;

    public class DockerUnmountResponse
    {
        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
