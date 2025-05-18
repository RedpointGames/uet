namespace Redpoint.Windows.HostNetworkingService
{
    using System.Text.Json.Serialization;

    public class HnsEndpointWithId : HnsEndpoint
    {
        [JsonPropertyName("ID")]
        public string Id { get; set; } = string.Empty;
    }
}
