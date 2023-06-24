namespace Redpoint.Uefs.Daemon.Integration.Docker.Models
{
    using System.Text.Json.Serialization;

    public class DockerVolumeInfo
    {
        [JsonPropertyName("Name")]
        public string Name = string.Empty;

        [JsonPropertyName("Mountpoint")]
        public string Mountpoint = string.Empty;

        // Status is not yet included.
    }
}
