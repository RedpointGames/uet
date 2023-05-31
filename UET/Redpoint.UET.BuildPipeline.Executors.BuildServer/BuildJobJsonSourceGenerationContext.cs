namespace Redpoint.UET.BuildPipeline.Executors.BuildServer
{
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(BuildJobJson))]
    public partial class BuildJobJsonSourceGenerationContext : JsonSerializerContext
    {
    }
}