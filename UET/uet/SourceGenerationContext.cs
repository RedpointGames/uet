namespace UET.Commands.EngineSpec
{
    using System.Text.Json.Serialization;
    using UET.Commands.ParameterSpec;
    using UET.Services;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UProjectFile))]
    [JsonSerializable(typeof(EngineBuildVersionJson))]
    [JsonSerializable(typeof(LauncherInstalled))]
    internal sealed partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
