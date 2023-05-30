namespace Redpoint.UET.BuildPipeline.Executors.BuildServer
{
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(BuildJobJson))]
    internal partial class BuildJobJsonSourceGenerationContext : JsonSerializerContext
    {
    }
}