namespace Redpoint.CredentialDiscovery
{
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(DockerConfigJson))]
    internal partial class DockerConfigJsonSourceGenerationContext : JsonSerializerContext
    {
    }
}