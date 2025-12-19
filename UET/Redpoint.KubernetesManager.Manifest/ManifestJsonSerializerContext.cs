namespace Redpoint.KubernetesManager.Manifests
{
    using Redpoint.KubernetesManager.Manifest;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(ContainerdManifest))]
    [JsonSerializable(typeof(KubeletManifest))]
    [JsonSerializable(typeof(PxeBootManifest))]
    [JsonSerializable(typeof(ActiveDirectoryManifest))]
    public partial class ManifestJsonSerializerContext : JsonSerializerContext
    {
    }
}
