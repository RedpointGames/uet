namespace Redpoint.KubernetesManager.Manifest
{
    using System.Text.Json.Serialization;

    public class ContainerdManifest : IVersionedManifest
    {
        [JsonIgnore]
        public static int ManifestCurrentVersion => 5;

        [JsonPropertyName("manifestVersion")]
        public required int ManifestVersion { get; set; }

        /// <summary>
        /// The directory under which one or more versions of containerd will be installed.
        /// </summary>
        [JsonPropertyName("containerdInstallRootPath")]
        public required string ContainerdInstallRootPath { get; set; }

        /// <summary>
        /// The directory where containerd should store all of it's state and configuration.
        /// </summary>
        [JsonPropertyName("containerdStatePath")]
        public required string ContainerdStatePath { get; set; }

        /// <summary>
        /// The containerd version to install.
        /// </summary>
        [JsonPropertyName("containerdVersion")]
        public required string ContainerdVersion { get; set; }

        /// <summary>
        /// If true, the patched version of containerd built by Redpoint will be used instead of the upstream version.
        /// </summary>
        [JsonPropertyName("useRedpointContainerd")]
        public required bool UseRedpointContainerd { get; set; }

        /// <summary>
        /// The runc version to install.
        /// </summary>
        [JsonPropertyName("runcVersion")]
        public required string RuncVersion { get; set; }

        /// <summary>
        /// The containerd path to serve the endpoint on.
        /// </summary>
        [JsonPropertyName("containerdEndpointPath")]
        public required string ContainerdEndpointPath { get; set; }

        /// <summary>
        /// The path at which to create a symlink to the CNI plugins directory, so that flanneld can be launched inside
        /// containers.
        /// </summary>
        [JsonPropertyName("cniPluginsSymlinkPath")]
        public required string CniPluginsSymlinkPath { get; set; }

        /// <summary>
        /// The version of the Flannel CNI plugins to use, from https://github.com/containernetworking/plugins. Should
        /// be a value like "1.7.1".
        /// </summary>
        [JsonPropertyName("cniPluginsVersion")]
        public required string CniPluginsVersion { get; set; }

        /// <summary>
        /// The version of Flannel to use, from https://github.com/flannel-io/flannel. Should be a value like
        /// "0.27.2".
        /// </summary>
        [JsonPropertyName("flannelVersion")]
        public required string FlannelVersion { get; set; }

        /// <summary>
        /// The flannel CNI version suffix of the Flannel download to use, from https://github.com/flannel-io/cni-plugin. Should
        /// be a value like "-flannel2".
        /// </summary>
        [JsonPropertyName("flannelCniVersionSuffix")]
        public required string FlannelCniVersionSuffix { get; set; }
    }
}
