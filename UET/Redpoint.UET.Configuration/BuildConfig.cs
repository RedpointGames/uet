namespace Redpoint.UET.Configuration
{
    using System.Text.Json.Serialization;

    public class BuildConfig
    {
        [JsonPropertyName("Type")]
        public BuildConfigType Type { get; set; }
    }
}
