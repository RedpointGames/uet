namespace Redpoint.Uet.BuildPipeline.BuildGraph.Export
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildGraphExport
    {
        [JsonPropertyName("Groups")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildGraphExportGroup[] Groups { get; set; } = Array.Empty<BuildGraphExportGroup>();
    }
}
