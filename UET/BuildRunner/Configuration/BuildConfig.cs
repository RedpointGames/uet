namespace BuildRunner.Configuration
{
    using System.Text.Json.Serialization;

    internal class BuildConfig
    {
        [JsonPropertyName("Type")]
        public BuildConfigType Type { get; set; }
    }
}
