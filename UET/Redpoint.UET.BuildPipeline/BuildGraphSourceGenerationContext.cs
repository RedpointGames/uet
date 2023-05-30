namespace Redpoint.UET.BuildPipeline
{
    using Redpoint.UET.BuildPipeline.BuildGraph.Export;
    using Redpoint.UET.BuildPipeline.BuildGraph.Patching;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(BuildGraphExport))]
    [JsonSerializable(typeof(BuildGraphPatchSet[]))]
    internal partial class BuildGraphSourceGenerationContext : JsonSerializerContext
    {
    }
}
