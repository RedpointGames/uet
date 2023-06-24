namespace Redpoint.Uefs.Daemon.Integration.Docker.Models
{
    using System.Text.Json.Serialization;

    public class DockerListResponse
    {
        [JsonPropertyName("Volumes")]
        public DockerVolumeInfo[] Volumes = new DockerVolumeInfo[0];

        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
