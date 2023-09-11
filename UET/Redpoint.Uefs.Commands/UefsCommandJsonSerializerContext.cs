namespace Redpoint.Uefs.Commands
{
    using Redpoint.Uefs.ContainerRegistry;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(RegistryImageConfig))]
    [JsonSerializable(typeof(RegistryReferenceInfo))]
    internal sealed partial class UefsCommandJsonSerializerContext : JsonSerializerContext
    {
    }
}
