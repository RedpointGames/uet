namespace Redpoint.KubernetesManager.Models
{
    using System.Text.Json.Serialization;

    internal class NodeManifest
    {
        /// <summary>
        /// Flannel is annoying and requires that the path it emits the flannel CNI plugin to be the same
        /// on all Linux machines in the cluster. To make this work, we have nodes create a symlink 
        /// under /opt/rkm/serverInstallationId to their actual installation location, so that flannel
        /// will write things into the correct location.
        /// </summary>
        [JsonPropertyName("serverRkmInstallationId")]
        public string ServerRKMInstallationId { get; set; } = string.Empty;

        [JsonPropertyName("nodeName")]
        public string NodeName { get; set; } = string.Empty;

        [JsonPropertyName("certificateAuthority")]
        public string CertificateAuthority { get; set; } = string.Empty;

        [JsonPropertyName("nodeCertificate")]
        public string NodeCertificate { get; set; } = string.Empty;

        [JsonPropertyName("nodeCertificateKey")]
        public string NodeCertificateKey { get; set; } = string.Empty;

        [JsonPropertyName("nodeKubeletConfig")]
        public string NodeKubeletConfig { get; set; } = string.Empty;
    }
}
