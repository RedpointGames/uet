using System.Text.Json.Serialization;

namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    public class UEFSMountInfo
    {
        [JsonPropertyName("Id")]
        public string Id = string.Empty;

        [JsonPropertyName("PackagePath")]
        public string PackagePath = string.Empty;

        [JsonPropertyName("MountPath")]
        public string MountPath = string.Empty;

        [JsonPropertyName("TagHint")]
        public string? TagHint = null;

        [JsonPropertyName("Persist"), Obsolete]
        public bool Persist = false;

        [JsonPropertyName("PersistMode")]
        public string PersistMode = "none";

        [JsonPropertyName("GitCommit")]
        public string GitCommit = string.Empty;

        [JsonPropertyName("GitUrl")]
        public string GitUrl = string.Empty;
    }
}
