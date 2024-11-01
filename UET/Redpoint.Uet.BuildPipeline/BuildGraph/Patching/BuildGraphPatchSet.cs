namespace Redpoint.Uet.BuildPipeline.BuildGraph.Patching
{
    using System.Text.Json.Serialization;

    internal record class BuildGraphPatchSet
    {
        [JsonPropertyName("File"), JsonRequired]
        public string File { get; set; } = string.Empty;

        [JsonPropertyName("Output4")]
        public string? Output4 { get; set; } = null;

        [JsonPropertyName("Output5"), JsonRequired]
        public string Output5 { get; set; } = string.Empty;

        [JsonPropertyName("Patches"), JsonRequired]
        public BuildGraphPatchSetPatch[] Patches { get; set; } = Array.Empty<BuildGraphPatchSetPatch>();
    }
}
