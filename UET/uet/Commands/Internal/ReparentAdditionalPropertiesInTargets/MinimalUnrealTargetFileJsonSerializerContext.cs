namespace UET.Commands.Internal.ReparentAdditionalPropertiesInTargets
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(MinimalUnrealTargetFile))]
    internal partial class MinimalUnrealTargetFileJsonSerializerContext : JsonSerializerContext
    {
    }
}
