namespace UET.Commands.Internal.PxeBoot
{
    using Redpoint.KubernetesManager.PxeBoot.Disk;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(PartedOutput))]
    internal partial class PartedJsonSerializerContext : JsonSerializerContext
    {
    }
}
