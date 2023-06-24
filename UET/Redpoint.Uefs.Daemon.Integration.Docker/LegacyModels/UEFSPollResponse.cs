using System.Text.Json.Serialization;

namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    public class UEFSPollResponse
    {
        [JsonPropertyName("Type")]
        public string Type = "package"; // or "git"

        [JsonPropertyName("Complete")]
        public bool Complete = false;

        [JsonPropertyName("Position")]
        public long Position = 0;

        [JsonPropertyName("Length")]
        public long Length = 0;

        [JsonPropertyName("Status")]
        public string Status = string.Empty;

        [JsonPropertyName("PackagePath")]
        public string? PackagePath = null;

        [JsonPropertyName("Err")]
        public string? Err = null;

        [JsonPropertyName("StartTime")]
        public DateTimeOffset StartTime = DateTimeOffset.UtcNow;

        [JsonPropertyName("VerifyPackageIndex")]
        public int? VerifyPackageIndex = null;

        [JsonPropertyName("VerifyPackageTotal")]
        public int? VerifyPackageTotal = null;

        [JsonPropertyName("VerifyChunksFixed")]
        public int? VerifyChunksFixed = null;

        [JsonPropertyName("GitServerProgressMessage")]
        public string? GitServerProgressMessage = null;

        [JsonPropertyName("GitTotalObjects")]
        public int? GitTotalObjects = null;

        [JsonPropertyName("GitIndexedObjects")]
        public int? GitIndexedObjects = null;

        [JsonPropertyName("GitReceivedObjects")]
        public int? GitReceivedObjects = null;

        [JsonPropertyName("GitReceivedBytes")]
        public long? GitReceivedBytes = null;

        [JsonPropertyName("GitSlowFetch")]
        public bool? GitSlowFetch = null;
    }
}
