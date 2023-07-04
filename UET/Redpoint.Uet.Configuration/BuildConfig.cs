namespace Redpoint.Uet.Configuration
{
    using System.Text.Json.Serialization;

    public class BuildConfig
    {
        [JsonPropertyName("UETVersion")]
        public string? UETVersion { get; set; }

        [JsonPropertyName("Type"), JsonRequired]
        public BuildConfigType Type { get; set; }
    }
}
