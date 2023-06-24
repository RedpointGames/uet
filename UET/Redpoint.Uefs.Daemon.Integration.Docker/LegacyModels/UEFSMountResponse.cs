using System.Text.Json.Serialization;

namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    public class UEFSMountResponse
    {
        [JsonPropertyName("Id")]
        public string Id = string.Empty;

        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
