namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.GooglePlay
{
    using System.Text.Json.Serialization;

    internal class PartnerCenterGameAssets
    {
        [JsonPropertyName("ekbFilePath"), JsonRequired]
        public required string EkbFilePath { get; set; }

        [JsonPropertyName("subValFilePath"), JsonRequired]
        public required string SubValFilePath { get; set; }

        [JsonPropertyName("symbolsFilePath"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required string? SymbolsFilePath { get; set; }

        [JsonPropertyName("discLayoutFilePath"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required string? DiscLayoutFilePath { get; set; }

        [JsonPropertyName("sodbFilePath"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required string? SodbFilePath { get; set; }
    }
}
