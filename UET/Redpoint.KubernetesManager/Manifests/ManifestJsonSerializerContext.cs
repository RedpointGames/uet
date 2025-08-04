namespace Redpoint.KubernetesManager.Manifests
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(ContainerdManifest))]
    [JsonSerializable(typeof(KubeletManifest))]
    public partial class ManifestJsonSerializerContext : JsonSerializerContext
    {
    }
}
