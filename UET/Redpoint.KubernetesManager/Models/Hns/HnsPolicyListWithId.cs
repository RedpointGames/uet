namespace Redpoint.KubernetesManager.Models.Hns
{
    using System.Text.Json.Serialization;

    internal class HnsPolicyListWithId : HnsPolicyList
    {
        [JsonPropertyName("ID")]
        public string Id { get; set; } = string.Empty;
    }
}
