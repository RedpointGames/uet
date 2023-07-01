namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    using System.Text.Json.Serialization;

    public class UEFSVerifyRequest
    {
        [JsonPropertyName("Fix")]
        public bool Fix = false;
    }
}
