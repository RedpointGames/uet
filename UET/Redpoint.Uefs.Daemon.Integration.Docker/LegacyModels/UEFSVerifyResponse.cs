namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    using System.Text.Json.Serialization;

    public class UEFSVerifyResponse
    {
        [JsonPropertyName("PollingId")]
        public string? PollingId = null;

        [JsonPropertyName("Err")]
        public string? Err = null;
    }
}
