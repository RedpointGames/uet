namespace Redpoint.Uet.Configuration.Engine
{
    using System.Text.Json.Serialization;

    public class BuildConfigEngineSource
    {
        [JsonPropertyName("Type"), JsonRequired]
        public string Type { get; set; } = "git";

        [JsonPropertyName("Repository"), JsonRequired]
        public string Repository { get; set; } = "git@github.com:EpicGames/UnrealEngine";

        [JsonPropertyName("Ref"), JsonRequired]
        public string Ref { get; set; } = "release";

        [JsonPropertyName("ConsoleZips")]
        public string[]? ConsoleZips { get; set; }
    }
}
