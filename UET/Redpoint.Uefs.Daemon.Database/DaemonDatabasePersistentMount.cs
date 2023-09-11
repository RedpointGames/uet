namespace Redpoint.Uefs.Daemon.Database
{
    using Redpoint.Uefs.Protocol;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class DaemonDatabasePersistentMount
    {
        [JsonPropertyName("packagePath")]
        public string? PackagePath { get; set; } = null;

        [JsonPropertyName("tagHint")]
        public string? TagHint { get; set; } = null;

        [JsonPropertyName("gitUrl")]
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "This property is used for JSON serialization.")]
        public string? GitUrl { get; set; } = null;

        [JsonPropertyName("gitCommit")]
        public string? GitCommit { get; set; } = null;

        [JsonPropertyName("gitWithLayers")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? GitWithLayers { get; set; } = null;

        [JsonPropertyName("writeStoragePath")]
        public string? WriteStoragePath { get; set; } = null;

        [JsonPropertyName("persistenceMode")]
        public WriteScratchPersistence PersistenceMode { get; set; } = WriteScratchPersistence.DiscardOnUnmount;

        [JsonPropertyName("gitHubToken")]
        public string? GitHubToken { get; set; } = null;

        [JsonPropertyName("gitHubOwner")]
        public string? GitHubOwner { get; set; } = null;

        [JsonPropertyName("gitHubRepo")]
        public string? GitHubRepo { get; set; } = null;

        [JsonPropertyName("folderSnapshotSourcePath")]
        public string? FolderSnapshotSourcePath { get; set; } = null;
    }
}
