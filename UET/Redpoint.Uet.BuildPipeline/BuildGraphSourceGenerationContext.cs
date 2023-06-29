namespace Redpoint.Uet.BuildPipeline
{
    using Redpoint.Uet.BuildPipeline.BuildGraph.Export;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Patching;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(BuildGraphExport))]
    [JsonSerializable(typeof(BuildGraphPatchSet[]))]
    internal partial class BuildGraphSourceGenerationContext : JsonSerializerContext
    {
    }
}
