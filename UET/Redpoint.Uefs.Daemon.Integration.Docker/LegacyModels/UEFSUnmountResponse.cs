namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    using System.Text.Json.Serialization;

    public class UEFSUnmountResponse
    {
        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
