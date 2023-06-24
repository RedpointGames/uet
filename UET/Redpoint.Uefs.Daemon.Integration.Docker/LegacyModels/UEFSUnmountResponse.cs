using System.Text.Json.Serialization;

namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    public class UEFSUnmountResponse
    {
        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
