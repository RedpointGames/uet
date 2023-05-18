namespace Redpoint.UET.BuildPipeline.BuildGraph.Patching
{
    using System.Text.Json.Serialization;

    internal record class BuildGraphPatchSet
    {
        [JsonPropertyName("File")]
        public required string File { get; set; }

        [JsonPropertyName("Output5")]
        public required string Output { get; set; }

        [JsonPropertyName("Patches")]
        public required BuildGraphPatchSetPatch[] Patches { get; set; }
    }
}
