namespace UET.Commands.Internal.RemoteZfsServer
{
    using System.Text.Json.Serialization;

    internal class RemoteZfsServerConfigTemplate
    {
        /// <summary>
        /// The template ID that remote callers will use to instantiate a workspace.
        /// </summary>
        [JsonPropertyName("TemplateId")]
        public required string TemplateId { get; set; }

        /// <summary>
        /// The name of the ZFS dataset whose latest snapshot will be used to instantiate the workspace.
        /// </summary>
        [JsonPropertyName("ZfsSnapshotDataset")]
        public required string ZfsSnapshotDataset { get; set; }

        /// <summary>
        /// The path under which the ZFS snapshot should be instantiated.
        /// </summary>
        [JsonPropertyName("LinuxParentDataset")]
        public required string LinuxParentDataset { get; set; }

        /// <summary>
        /// The Windows network share directory that the ZFS snapshot will appear under once instantiated (i.e. this should be the Windows network share that exposes the directory referred to by LinuxParentDataset).
        /// </summary>
        [JsonPropertyName("WindowsNetworkShareParentPath")]
        public required string WindowsNetworkShareParentPath { get; set; }
    }
}
