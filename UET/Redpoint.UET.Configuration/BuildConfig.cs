namespace Redpoint.UET.Configuration
{
    using System.Text.Json.Serialization;

    public class BuildConfig
    {
        [JsonPropertyName("UETVersion")]
        public string? UETVersion { get; set; }

        [JsonPropertyName("Type")]
        public BuildConfigType Type { get; set; }
    }
}
