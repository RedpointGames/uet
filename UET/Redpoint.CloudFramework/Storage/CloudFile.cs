namespace Redpoint.CloudFramework.Storage
{
    using Newtonsoft.Json;
    using System.Text.Json.Serialization;

    public class CloudFile
    {
        [JsonProperty("fileId"), JsonPropertyName("fileId")]
        public string FileId { get; set; } = string.Empty;

        [JsonProperty("filename"), JsonPropertyName("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonProperty("size"), JsonPropertyName("size")]
        public long Size { get; internal set; }
    }
}
