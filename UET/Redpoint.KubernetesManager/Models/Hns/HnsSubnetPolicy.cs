namespace Redpoint.KubernetesManager.Models.Hns
{
    using System.Text.Json.Serialization;

    internal class HnsSubnetPolicy
    {
        [JsonPropertyName("Type")]
        public string? Type { get; set; } = null;

        [JsonPropertyName("VSID")]
        public int? VSID { get; set; } = null;
    }
}
