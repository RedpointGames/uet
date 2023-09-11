namespace Redpoint.Uet.Configuration.Engine
{
    using System.Text.Json.Serialization;

    public class BuildConfigEngineBuild
    {
        [JsonPropertyName("TargetTypes")]
        public string[] TargetTypes { get; set; } = Array.Empty<string>();

        [JsonPropertyName("EditorPlatforms")]
        public string[] EditorPlatforms { get; set; } = Array.Empty<string>();

        [JsonPropertyName("Platforms")]
        public string[] Platforms { get; set; } = Array.Empty<string>();
    }
}
