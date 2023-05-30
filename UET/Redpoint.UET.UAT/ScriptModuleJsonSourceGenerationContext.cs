namespace Redpoint.UET.UAT
{
    using System.Text.Json.Serialization;
    using static Redpoint.UET.UAT.DefaultUATExecutor;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ScriptModuleJson))]
    internal partial class ScriptModuleJsonSourceGenerationContext : JsonSerializerContext
    {
    }
}