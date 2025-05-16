namespace Redpoint.KubernetesManager.Models.Hns
{
    using System.Text.Json.Serialization;

    internal class HnsNetworkWithId : HnsNetwork
    {
        [JsonPropertyName("ID")]
        public string Id { get; set; } = string.Empty;
    }
}
