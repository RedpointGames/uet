namespace Redpoint.KubernetesManager.Manifests
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(ContainerdManifest))]
    public partial class ManifestJsonSerializerContext : JsonSerializerContext
    {
    }
}
