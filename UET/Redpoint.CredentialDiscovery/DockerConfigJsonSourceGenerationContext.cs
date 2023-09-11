namespace Redpoint.CredentialDiscovery
{
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(DockerConfigJson))]
    internal sealed partial class DockerConfigJsonSourceGenerationContext : JsonSerializerContext
    {
    }
}