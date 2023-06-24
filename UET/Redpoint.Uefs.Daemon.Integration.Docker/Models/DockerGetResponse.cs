namespace Redpoint.Uefs.Daemon.Integration.Docker.Models
{
    using System.Text.Json.Serialization;

    public class DockerGetResponse
    {
        [JsonPropertyName("Volume")]
        public DockerVolumeInfo Volume = new DockerVolumeInfo();

        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
