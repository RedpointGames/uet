namespace UET.Services
{
    using System.Text.Json.Serialization;

    internal sealed class EngineBuildVersionJson
    {
        [JsonPropertyName("MajorVersion")]
        public int MajorVersion { get; set; }

        [JsonPropertyName("MinorVersion")]
        public int MinorVersion { get; set; }
    }
}
