using System.Text.Json.Serialization;

namespace Redpoint.Uefs.ContainerRegistry
{
    [JsonSerializable(typeof(DockerConfigJson))]
    internal partial class UefsRegistryInternalJsonSerializerContext : JsonSerializerContext
    {
    }
}
