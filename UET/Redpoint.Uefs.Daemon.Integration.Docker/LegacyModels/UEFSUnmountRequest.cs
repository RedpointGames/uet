using System.Text.Json.Serialization;

namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    public class UEFSUnmountRequest
    {
        [JsonPropertyName("Id")]
        public string Id = string.Empty;
    }
}
