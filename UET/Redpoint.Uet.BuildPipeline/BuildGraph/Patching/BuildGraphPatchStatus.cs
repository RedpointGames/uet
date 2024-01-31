namespace Redpoint.Uet.BuildPipeline.BuildGraph.Patching
{
    using System.Text.Json.Serialization;

    internal record class BuildGraphPatchStatus
    {
        [JsonPropertyName("patchHash")]
        public string PatchHash { get; set; } = string.Empty;

        [JsonPropertyName("patchCodeVersion")]
        public int PatchCodeVersion { get; set; }

        [JsonPropertyName("buildGraphAutomationDllLastModified")]
        public long BuildGraphAutomationDllLastModified { get; set; }
    }
}