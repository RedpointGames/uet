namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Text.Json.Serialization;

    public class RkmConfigurationComponentVersions
    {
        [JsonPropertyName("rkm")]
        public string? Rkm { get; set; }

        [JsonPropertyName("containerd")]
        public string? Containerd { get; set; }

        [JsonPropertyName("runc")]
        public string? Runc { get; set; }

        [JsonPropertyName("kubernetes")]
        public string? Kubernetes { get; set; }

        [JsonPropertyName("etcd")]
        public string? Etcd { get; set; }

        [JsonPropertyName("cniPlugins")]
        public string? CniPlugins { get; set; }

        [JsonPropertyName("flannel")]
        public string? Flannel { get; set; }

        [JsonPropertyName("flannelCniSuffix")]
        public string? FlannelCniSuffix { get; set; }
    }
}
