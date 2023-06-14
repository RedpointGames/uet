using System.Text.Json.Serialization;

namespace Redpoint.Uefs.ContainerRegistry
{
    /// <summary>
    /// The <see cref="JsonSerializerContext"/> for JSON serializable types provided by this library.
    /// </summary>
    [JsonSerializable(typeof(DockerConfigJson))]
    [JsonSerializable(typeof(RegistryCredential))]
    [JsonSerializable(typeof(RegistryReferenceInfo))]
    [JsonSerializable(typeof(RegistryImageConfig))]
    public partial class UefsRegistryJsonSerializerContext : JsonSerializerContext
    {
    }
}
