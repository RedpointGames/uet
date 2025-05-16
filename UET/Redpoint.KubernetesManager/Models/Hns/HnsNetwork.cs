namespace Redpoint.KubernetesManager.Models.Hns
{
    using System.Text.Json.Serialization;

    internal class HnsNetwork
    {
        [JsonPropertyName("Type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("Name")]
        public string? Name { get; set; } = null;

        [JsonPropertyName("Subnets")]
        public HnsSubnet[] Subnets { get; set; } = new HnsSubnet[0];

        [JsonPropertyName("NetworkAdapterName")]
        public string? NetworkAdapterName { get; set; } = null;
    }
}
