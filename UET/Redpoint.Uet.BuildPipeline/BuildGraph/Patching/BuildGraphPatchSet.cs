namespace Redpoint.Uet.BuildPipeline.BuildGraph.Patching
{
    using System.Text.Json.Serialization;

    internal record class BuildGraphPatchSet
    {
        [JsonPropertyName("File"), JsonRequired]
        public string File { get; set; } = string.Empty;

        [JsonPropertyName("Output5"), JsonRequired]
        public string Output { get; set; } = string.Empty;

        [JsonPropertyName("Patches"), JsonRequired]
        public BuildGraphPatchSetPatch[] Patches { get; set; } = new BuildGraphPatchSetPatch[0];
    }
}
