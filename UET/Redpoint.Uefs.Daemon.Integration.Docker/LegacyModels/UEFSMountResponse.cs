namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    using System.Text.Json.Serialization;

    public class UEFSMountResponse
    {
        [JsonPropertyName("Id")]
        public string Id = string.Empty;

        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
