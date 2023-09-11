namespace Redpoint.Uefs.Daemon.Integration.Docker.Models
{
    using System.Text.Json.Serialization;

    public class DockerListResponse
    {
        [JsonPropertyName("Volumes")]
        public DockerVolumeInfo[] Volumes = Array.Empty<DockerVolumeInfo>();

        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
