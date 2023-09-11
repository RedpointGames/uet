namespace Redpoint.Uet.BuildPipeline.BuildGraph.Export
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildGraphExportGroup
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Agent Types")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[] AgentTypes { get; set; } = Array.Empty<string>();

        [JsonPropertyName("Nodes")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildGraphExportNode[] Nodes { get; set; } = Array.Empty<BuildGraphExportNode>();
    }
}
