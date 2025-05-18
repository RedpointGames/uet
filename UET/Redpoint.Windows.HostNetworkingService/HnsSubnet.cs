namespace Redpoint.Windows.HostNetworkingService
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class HnsSubnet
    {
        [JsonPropertyName("AddressPrefix")]
        public string? AddressPrefix { get; set; } = null;

        [JsonPropertyName("GatewayAddress")]
        public string? GatewayAddress { get; set; } = null;

        [JsonPropertyName("Policies")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public HnsSubnetPolicy[] Policies { get; set; } = Array.Empty<HnsSubnetPolicy>();
    }
}
