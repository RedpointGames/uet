namespace Redpoint.UET.BuildPipeline.BuildGraph.Export
{
    using System.Text.Json.Serialization;

    internal class BuildGraphExportGroup
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Agent Types")]
        public string[] AgentTypes { get; set; } = new string[0];

        [JsonPropertyName("Nodes")]
        public BuildGraphExportNode[] Nodes { get; set; } = new BuildGraphExportNode[0];
    }
}
