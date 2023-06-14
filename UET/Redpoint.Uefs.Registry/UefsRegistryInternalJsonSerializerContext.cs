using System.Text.Json.Serialization;

namespace uefs.Registry
{
    [JsonSerializable(typeof(DockerConfigJson))]
    internal partial class UefsRegistryInternalJsonSerializerContext : JsonSerializerContext
    {
    }
}
