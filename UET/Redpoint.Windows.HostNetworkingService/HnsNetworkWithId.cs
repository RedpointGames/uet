namespace Redpoint.Windows.HostNetworkingService
{
    using System.Text.Json.Serialization;

    public class HnsNetworkWithId : HnsNetwork
    {
        [JsonPropertyName("ID")]
        public string Id { get; set; } = string.Empty;
    }
}
