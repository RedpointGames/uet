namespace Redpoint.KubernetesManager.Models.Hns
{
    using System.Text.Json.Serialization;

    internal class HnsSubnet
    {
        [JsonPropertyName("AddressPrefix")]
        public string? AddressPrefix { get; set; } = null;

        [JsonPropertyName("GatewayAddress")]
        public string? GatewayAddress { get; set; } = null;

        [JsonPropertyName("Policies")]
        public HnsSubnetPolicy[] Policies { get; set; } = new HnsSubnetPolicy[0];
    }
}
