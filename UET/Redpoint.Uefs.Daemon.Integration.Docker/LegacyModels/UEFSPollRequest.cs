namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    using System.Text.Json.Serialization;

    public class UEFSPollRequest
    {
        [JsonPropertyName("PollingId")]
        public string PollingId = string.Empty;
    }
}
