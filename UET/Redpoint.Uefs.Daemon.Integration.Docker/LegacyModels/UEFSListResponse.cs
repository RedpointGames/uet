namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    using System.Text.Json.Serialization;

    public class UEFSListResponse
    {
        [JsonPropertyName("Mounts")]
        public List<UEFSMountInfo> Mounts = new List<UEFSMountInfo>();

        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
