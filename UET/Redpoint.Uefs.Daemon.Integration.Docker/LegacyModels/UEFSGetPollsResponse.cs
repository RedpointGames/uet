using System.Text.Json.Serialization;

namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    public class UEFSGetPollsResponse
    {
        // Map of polling IDs to the tag being pulled.
        [JsonPropertyName("PollingMap")]
        public Dictionary<string, string> PollingMap = new Dictionary<string, string>();

        [JsonPropertyName("Err")]
        public string? Err = null;
    }
}
