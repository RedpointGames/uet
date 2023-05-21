namespace Redpoint.UET.BuildPipeline.BuildGraph.Export
{
    using System.Text.Json.Serialization;

    public class BuildGraphExportNotify
    {
        [JsonPropertyName("Default")]
        public string Default { get; set; } = string.Empty;

        [JsonPropertyName("Submitters")]
        public string Submitters { get; set; } = string.Empty;

        [JsonPropertyName("Warnings")]
        public bool Warnings { get; set; } = true;
    }
}
