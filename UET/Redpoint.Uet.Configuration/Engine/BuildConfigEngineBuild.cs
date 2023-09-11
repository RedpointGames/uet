namespace Redpoint.Uet.Configuration.Engine
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigEngineBuild
    {
        [JsonPropertyName("TargetTypes")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[] TargetTypes { get; set; } = Array.Empty<string>();

        [JsonPropertyName("EditorPlatforms")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[] EditorPlatforms { get; set; } = Array.Empty<string>();

        [JsonPropertyName("Platforms")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[] Platforms { get; set; } = Array.Empty<string>();
    }
}
