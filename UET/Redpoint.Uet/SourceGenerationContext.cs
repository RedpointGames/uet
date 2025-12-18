namespace UET.Commands.EngineSpec
{
    using Redpoint.Uet.Commands.ParameterSpec;
    using Redpoint.Uet.Services;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UProjectFile))]
    [JsonSerializable(typeof(EngineBuildVersionJson))]
    [JsonSerializable(typeof(LauncherInstalled))]
    internal sealed partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
