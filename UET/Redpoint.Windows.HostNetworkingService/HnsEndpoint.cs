namespace Redpoint.Windows.HostNetworkingService
{
    using System.Text.Json.Serialization;

    public class HnsEndpoint
    {
        [JsonPropertyName("Name")]
        public string? Name { get; set; } = null;

        [JsonPropertyName("IPAddress")]
        public string? IPAddress { get; set; } = null;
    }
}
