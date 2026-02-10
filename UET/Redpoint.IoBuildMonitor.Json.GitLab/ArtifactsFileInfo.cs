using System.Text.Json.Serialization;

namespace Io.Json.GitLab
{
    public class ArtifactsFileInfo
    {
        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }
    }
}
