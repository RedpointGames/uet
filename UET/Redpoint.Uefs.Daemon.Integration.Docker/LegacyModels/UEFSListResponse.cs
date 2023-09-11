namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class UEFSListResponse
    {
        [JsonPropertyName("Mounts")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "This field is used in JSON serialization.")]
        public List<UEFSMountInfo> Mounts = new List<UEFSMountInfo>();

        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
