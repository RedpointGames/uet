namespace Redpoint.KubernetesManager.Manifest
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class KubeletManifest : IVersionedManifest
    {
        /**
         * The constructor magic below ensures the JSON deserializer does not require 'required' properties to be set on deserialization, while still requiring us to set them when constructing manifests. This ensures backwards compatibility when attempting to deserialize newer manifests.
         */

#pragma warning disable CS8618
        [JsonConstructor]
        [SetsRequiredMembers]
        internal KubeletManifest(int manifestVersion)
        {
            ManifestVersion = manifestVersion;
        }
#pragma warning restore CS8618

        public KubeletManifest()
        {
        }

        [JsonIgnore]
        public static int ManifestCurrentVersion => 2;

        [JsonPropertyName("manifestVersion")]
        public required int ManifestVersion { get; set; }

        /// <summary>
        /// The directory under which one or more versions of kubelet will be installed.
        /// </summary>
        [JsonPropertyName("kubeletInstallRootPath")]
        public required string KubeletInstallRootPath { get; set; }

        /// <summary>
        /// The directory where kubelet should store all of it's state and configuration.
        /// </summary>
        [JsonPropertyName("kubeletStatePath")]
        public required string KubeletStatePath { get; set; }

        /// <summary>
        /// The Kubernetes version to install.
        /// </summary>
        [JsonPropertyName("kubernetesVersion")]
        public required string KubernetesVersion { get; set; }

        /// <summary>
        /// The cluster domain.
        /// </summary>
        [JsonPropertyName("clusterDomain")]
        public required string ClusterDomain { get; set; }

        /// <summary>
        /// The cluster DNS.
        /// </summary>
        [JsonPropertyName("clusterDns")]
        public required string ClusterDns { get; set; }

        /// <summary>
        /// The containerd endpoint to connect to.
        /// </summary>
        [JsonPropertyName("containerdEndpoint")]
        public required string ContainerdEndpoint { get; set; }

        /// <summary>
        /// The certificate authority data.
        /// </summary>
        [JsonPropertyName("caCertData")]
        public required string CaCertData { get; set; }

        /// <summary>
        /// The node certificate data.
        /// </summary>
        [JsonPropertyName("nodeCertData")]
        public required string NodeCertData { get; set; }

        /// <summary>
        /// The node private key data.
        /// </summary>
        [JsonPropertyName("nodeKeyData")]
        public required string NodeKeyData { get; set; }

        /// <summary>
        /// The address of the API server.
        /// </summary>
        [JsonPropertyName("apiServerAddress")]
        public required string ApiServerAddress { get; set; }

        /// <summary>
        /// The etcd version to install.
        /// </summary>
        [JsonPropertyName("etcdVersion")]
        public required string EtcdVersion { get; set; }
    }
}
