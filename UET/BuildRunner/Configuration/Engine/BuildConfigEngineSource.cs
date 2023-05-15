namespace BuildRunner.Configuration.Engine
{
    using System.Text.Json.Serialization;

    internal class BuildConfigEngineSource
    {
        [JsonPropertyName("Type")]
        public string Type { get; set; } = "git";

        [JsonPropertyName("Repository")]
        public string Repository { get; set; } = "git@github.com:EpicGames/UnrealEngine";

        [JsonPropertyName("Commit")]
        public string Commit { get; set; } = "release";
    }
}
