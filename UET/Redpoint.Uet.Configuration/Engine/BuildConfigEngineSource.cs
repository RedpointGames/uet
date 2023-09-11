namespace Redpoint.Uet.Configuration.Engine
{
    using System.Diagnostics.CodeAnalysis;
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
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? ConsoleZips { get; set; }
    }
}
