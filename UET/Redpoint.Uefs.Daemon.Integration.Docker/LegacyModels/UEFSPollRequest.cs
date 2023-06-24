using System.Text.Json.Serialization;

namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    public class UEFSPollRequest
    {
        [JsonPropertyName("PollingId")]
        public string PollingId = string.Empty;
    }
}
