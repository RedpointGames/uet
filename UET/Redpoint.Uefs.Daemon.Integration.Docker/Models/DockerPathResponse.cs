namespace Redpoint.Uefs.Daemon.Integration.Docker.Models
{
    using System.Text.Json.Serialization;

    public class DockerPathResponse
    {
        [JsonPropertyName("Mountpoint")]
        public string Mountpoint = string.Empty;

        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
