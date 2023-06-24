using System.Text.Json.Serialization;

namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    public class UEFSVerifyRequest
    {
        [JsonPropertyName("Fix")]
        public bool Fix = false;
    }
}
