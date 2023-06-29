namespace UET.Commands.EngineSpec
{
    using System.Text.Json.Serialization;
    using UET.Services;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UProjectFile))]
    [JsonSerializable(typeof(EngineBuildVersionJson))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
