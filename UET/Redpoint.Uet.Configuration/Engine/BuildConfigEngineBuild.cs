namespace Redpoint.Uet.Configuration.Engine
{
    using System.Text.Json.Serialization;

    public class BuildConfigEngineBuild
    {
        [JsonPropertyName("TargetTypes")]
        public string[] TargetTypes { get; set; } = new string[0];

        [JsonPropertyName("EditorPlatforms")]
        public string[] EditorPlatforms { get; set; } = new string[0];

        [JsonPropertyName("Platforms")]
        public string[] Platforms { get; set; } = new string[0];
    }
}
