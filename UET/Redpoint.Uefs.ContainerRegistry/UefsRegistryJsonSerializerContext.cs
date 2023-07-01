namespace Redpoint.Uefs.ContainerRegistry
{
    using System.Text.Json.Serialization;

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
