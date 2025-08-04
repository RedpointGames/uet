namespace Redpoint.KubernetesManager.Manifests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public class ContainerdManifest : IVersionedManifest
    {
        [JsonIgnore]
        public static int ManifestCurrentVersion => 2;

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
        /// The directory where CNI plugin binaries are installed.
        /// </summary>
        [JsonPropertyName("cniPluginsPath")]
        public required string CniPluginsPath { get; set; }

        /// <summary>
        /// The containerd path to serve the endpoint on.
        /// </summary>
        [JsonPropertyName("containerdEndpointPath")]
        public required string ContainerdEndpointPath { get; set; }
    }
}
