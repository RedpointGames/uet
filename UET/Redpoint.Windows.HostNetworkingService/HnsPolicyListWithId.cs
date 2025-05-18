namespace Redpoint.Windows.HostNetworkingService
{
    using System.Text.Json.Serialization;

    public class HnsPolicyListWithId : HnsPolicyList
    {
        [JsonPropertyName("ID")]
        public string Id { get; set; } = string.Empty;
    }
}
