using System.Text.Json.Serialization;

namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    public class UEFSListResponse
    {
        [JsonPropertyName("Mounts")]
        public List<UEFSMountInfo> Mounts = new List<UEFSMountInfo>();

        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
