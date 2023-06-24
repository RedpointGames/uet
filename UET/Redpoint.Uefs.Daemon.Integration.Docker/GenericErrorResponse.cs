namespace Redpoint.Uefs.Daemon.Integration.Docker
{
    using System.Text.Json.Serialization;

    public class GenericErrorResponse
    {
        [JsonPropertyName("Err")]
        public string Err = string.Empty;
    }
}
