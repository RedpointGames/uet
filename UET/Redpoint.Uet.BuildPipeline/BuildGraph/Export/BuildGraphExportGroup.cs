namespace Redpoint.Uet.BuildPipeline.BuildGraph.Export
{
    using System.Text.Json.Serialization;

    public class BuildGraphExportGroup
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Agent Types")]
        public string[] AgentTypes { get; set; } = Array.Empty<string>();

        [JsonPropertyName("Nodes")]
        public BuildGraphExportNode[] Nodes { get; set; } = Array.Empty<BuildGraphExportNode>();
    }
}
