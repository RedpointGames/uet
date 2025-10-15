namespace Redpoint.CloudFramework.Storage
{
    using System.Text.Json.Serialization;

    public class CloudFile
    {
        [JsonPropertyName("fileId")]
        public string FileId { get; set; } = string.Empty;

        [JsonPropertyName("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; internal set; }
    }
}
