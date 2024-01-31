namespace Redpoint.Uet.BuildPipeline.BuildGraph.Patching
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(BuildGraphPatchStatus))]
    internal partial class BuildGraphPatchStatusJsonSerializerContext : JsonSerializerContext
    {
    }
}