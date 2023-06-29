namespace Redpoint.Uet.BuildPipeline.BuildGraph.Export
{
    using System.Text.Json.Serialization;

    public class BuildGraphExport
    {
        [JsonPropertyName("Groups")]
        public BuildGraphExportGroup[] Groups { get; set; } = new BuildGraphExportGroup[0];
    }
}
