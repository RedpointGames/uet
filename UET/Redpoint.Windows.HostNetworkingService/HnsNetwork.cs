namespace Redpoint.Windows.HostNetworkingService
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class HnsNetwork
    {
        [JsonPropertyName("Type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("Name")]
        public string? Name { get; set; } = null;

        [JsonPropertyName("Subnets")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public HnsSubnet[] Subnets { get; set; } = Array.Empty<HnsSubnet>();

        [JsonPropertyName("NetworkAdapterName")]
        public string? NetworkAdapterName { get; set; } = null;
    }
}
