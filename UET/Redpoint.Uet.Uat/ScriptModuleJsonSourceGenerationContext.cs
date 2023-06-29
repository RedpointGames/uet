namespace Redpoint.Uet.Uat
{
    using System.Text.Json.Serialization;
    using static Redpoint.Uet.Uat.DefaultUATExecutor;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ScriptModuleJson))]
    internal partial class ScriptModuleJsonSourceGenerationContext : JsonSerializerContext
    {
    }
}