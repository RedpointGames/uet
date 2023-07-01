namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    using System.Text.Json.Serialization;

    public class UEFSUnmountRequest
    {
        [JsonPropertyName("Id")]
        public string Id = string.Empty;
    }
}
