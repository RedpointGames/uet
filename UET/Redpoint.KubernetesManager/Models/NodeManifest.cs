namespace Redpoint.KubernetesManager.Models
{
    using YamlDotNet.Serialization;

    [YamlSerializable]
    internal class NodeManifest
    {
        /// <summary>
        /// Flannel is annoying and requires that the path it emits the flannel CNI plugin to be the same
        /// on all Linux machines in the cluster. To make this work, we have nodes create a symlink 
        /// under /opt/rkm/serverInstallationId to their actual installation location, so that flannel
        /// will write things into the correct location.
        /// </summary>
        [YamlMember(Alias = "server-rkm-installation-id")]
        public string ServerRKMInstallationId { get; set; } = string.Empty;

        [YamlMember(Alias = "node-name")]
        public string NodeName { get; set; } = string.Empty;

        [YamlMember(Alias = "certificate-authority")]
        public string CertificateAuthority { get; set; } = string.Empty;

        [YamlMember(Alias = "node-certificate")]
        public string NodeCertificate { get; set; } = string.Empty;

        [YamlMember(Alias = "node-certificate-key")]
        public string NodeCertificateKey { get; set; } = string.Empty;

        [YamlMember(Alias = "node-kubelet-config")]
        public string NodeKubeletConfig { get; set; } = string.Empty;
    }
}
