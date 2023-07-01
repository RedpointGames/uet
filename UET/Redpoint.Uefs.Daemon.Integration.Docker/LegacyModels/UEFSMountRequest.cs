namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    using System.Text.Json.Serialization;

    public class UEFSMountRequest
    {
        /// <summary>
        /// If set, mounts the specified VHD or sparse image located at the given path.
        /// </summary>
        [JsonPropertyName("PackagePath")]
        public string? PackagePath = null;

        /// <summary>
        /// The path to mount the package or Git commit at.
        /// </summary>
        [JsonPropertyName("MountPath")]
        public string MountPath = string.Empty;

        /// <summary>
        /// If set, the daemon will track the lifetime of the process with this ID, and when that
        /// process exits, the daemon will automatically unmount this mount.
        /// </summary>
        [JsonPropertyName("TrackPid")]
        public int? TrackPid = null;

        /// <summary>
        /// The persistent mode for the mount. Should be one of 'none', 'ro' or 'rw'.
        /// </summary>
        [JsonPropertyName("PersistMode")]
        public string? PersistMode = "none";

        /// <summary>
        /// If the client pulled this package with a tag (via the pull and poll APIs), it should
        /// provide the tag here. This is purely so that the 'list' command can provide a useful
        /// alias (instead of the hashed package path). It does not affect the mount behaviour.
        /// </summary>
        [JsonPropertyName("TagHint")]
        public string? TagHint = null;

        /// <summary>
        /// If set, mounts a native Git repository by fetching the specified <see cref="GitCommit"/>
        /// from the repository.
        /// </summary>
        [JsonPropertyName("GitUrl")]
        public string? GitUrl = null;

        /// <summary>
        /// The Git commit to mount; for use with either <see cref="GitCommit"/> or <see cref="GitHubRepo"/>.
        /// </summary>
        [JsonPropertyName("GitCommit")]
        public string? GitCommit = null;

        /// <summary>
        /// A list of absolute paths to layer on top of the Git mount.
        /// </summary>
        [JsonPropertyName("ConsoleLayers")]
        public string[]? ConsoleLayers = null;

        /// <summary>
        /// For read-write persistent mounts, this is the path to the VHD differencing layer that contains the
        /// persisted write data. This parameter is only used internally by the daemon at startup and should
        /// not be used by external clients.
        /// </summary>
        [JsonPropertyName("PreviousWriteStoragePath")]
        public string? PreviousWriteStoragePath = null;

        /// <summary>
        /// If set, overrides the ID used to track this mount. This is really only used by the Kubernetes CSI
        /// driver, where Kubernetes determines the volume ID instead of the UEFS daemon.
        /// </summary>
        [JsonPropertyName("OverrideId")]
        public string? OverrideId = null;

        /// <summary>
        /// If set, mounts a GitHub repository directly without having to fetch all the data first.
        /// The GitHub REST API is used to fetch data on demand. If set, you must also set
        /// <see cref="GitHubRepo"/> and <see cref="GitHubToken"/>.
        /// </summary>
        [JsonPropertyName("GitHubOwner")]
        public string? GitHubOwner = null;

        /// <summary>
        /// The GitHub repository name to mount.
        /// </summary>
        [JsonPropertyName("GitHubRepo")]
        public string? GitHubRepo = null;

        /// <summary>
        /// The GitHub token to use to authenticate with the GitHub REST API.
        /// </summary>
        [JsonPropertyName("GitHubToken")]
        public string? GitHubToken = null;

        /// <summary>
        /// The path that scratch (copy-on-write) data will be stored. If this is not set, a 
        /// scratch path is generated and cleaned up on dismount.
        /// </summary>
        [JsonPropertyName("ScratchPath")]
        public string? ScratchPath = null;

        /// <summary>
        /// The path that will be "snapshotted" (not really) and used as the basis for the new
        /// mount. All writes will go to the scratch (copy-on-write) path.
        /// </summary>
        [JsonPropertyName("FolderSnapshotPath")]
        public string? FolderSnapshotPath = null;
    }
}
