namespace Redpoint.UET.BuildPipeline.BuildGraph.Patching
{
    using System.Text.Json.Serialization;

    internal record class BuildGraphPatchSetPatch
    {
        [JsonPropertyName("Mode")]
        public string Mode { get; set; } = "FindReplace";

        [JsonPropertyName("Find")]
        public string? Find { get; set; }

        [JsonPropertyName("Replace")]
        public string? Replace { get; set; }

        [JsonPropertyName("Contains")]
        public string? Contains { get; set; }

        [JsonPropertyName("StartIndex")]
        public string? StartIndex { get; set; }

        [JsonPropertyName("EndIndex")]
        public string? EndIndex { get; set; }

        [JsonPropertyName("HandleWindowsNewLines")]
        public bool HandleWindowsNewLines { get; set; } = false;
    }
}
