namespace Redpoint.KubernetesManager.Manifest
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class NodeManifest : IVersionedManifest
    {
        /**
         * The constructor magic below ensures the JSON deserializer does not require 'required' properties to be set on deserialization, while still requiring us to set them when constructing manifests. This ensures backwards compatibility when attempting to deserialize newer manifests.
         */

#pragma warning disable CS8618
        [JsonConstructor]
        [SetsRequiredMembers]
        internal NodeManifest(int manifestVersion)
        {
            ManifestVersion = manifestVersion;
        }
#pragma warning restore CS8618

        public NodeManifest()
        {
        }

        [JsonIgnore]
        public static int ManifestCurrentVersion => 1;

        [JsonPropertyName("manifestVersion")]
        public required int ManifestVersion { get; set; }

        /// <summary>
        /// The static pods that the kubelet on this node should run. {RKM_ROOT} and {LOCAL_IP_ADDRESS} will be replaced in the template before the node's RKM service sends to the Kubelet service.
        /// </summary>
        [JsonPropertyName("staticPodsTemplateYaml")]
        public required string StaticPodsTemplateYaml { get; set; }

        /// <summary>
        /// The containerd version to run on the node.
        /// </summary>
        [JsonPropertyName("containerdVersion")]
        public required string ContainerdVersion { get; set; }

        /// <summary>
        /// The Kubernetes version of Kubelet to run on the node.
        /// </summary>
        [JsonPropertyName("kubernetesVersion")]
        public required string KubernetesVersion { get; set; }

        /// <summary>
        /// The runc version to run on the node; only used by Linux nodes.
        /// </summary>
        [JsonPropertyName("runcVersion")]
        public required string RuncVersion { get; set; }

        /// <summary>
        /// The version of the Flannel CNI plugins to run on this node.
        /// </summary>
        [JsonPropertyName("cniPluginsVersion")]
        public required string CniPluginsVersion { get; set; }

        /// <summary>
        /// The version of Flannel to run on this node.
        /// </summary>
        [JsonPropertyName("flannelVersion")]
        public required string FlannelVersion { get; set; }

        /// <summary>
        /// The flannel CNI version suffix of the Flannel download to run on this node.
        /// </summary>
        [JsonPropertyName("flannelCniVersionSuffix")]
        public required string FlannelCniVersionSuffix { get; set; }

        /// <summary>
        /// The cluster domain used by CoreDNS.
        /// </summary>
        [JsonPropertyName("clusterDnsDomain")]
        public required string ClusterDnsDomain { get; set; }

        /// <summary>
        /// The IP address of CoreDNS in the cluster.
        /// </summary>
        [JsonPropertyName("clusterDnsServerIpAddress")]
        public required string ClusterDnsServerIpAddress { get; set; }

        /// <summary>
        /// The certificate authority public PEM to pass to Kubelet.
        /// </summary>
        [JsonPropertyName("certificateAuthorityPem")]
        public required string CertificateAuthorityPem { get; set; }

        /// <summary>
        /// The node's public PEM to pass to Kubelet.
        /// </summary>
        [JsonPropertyName("nodeCertificatePem")]
        public required string NodeCertificatePem { get; set; }

        /// <summary>
        /// The node's private key PEM to pass to Kubelet.
        /// </summary>
        [JsonPropertyName("nodePrivateKeyPem")]
        public required string NodePrivateKeyPem { get; set; }
    }
}
