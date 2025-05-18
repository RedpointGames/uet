namespace Redpoint.Windows.HostNetworkingService
{
    using System.Text.Json.Serialization;

    public class HnsSubnetPolicy
    {
        [JsonPropertyName("Type")]
        public string? Type { get; set; } = null;

        [JsonPropertyName("VSID")]
        public int? VSID { get; set; } = null;
    }
}
