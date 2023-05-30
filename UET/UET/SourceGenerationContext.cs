namespace UET.Commands.EngineSpec
{
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UProjectFile))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
