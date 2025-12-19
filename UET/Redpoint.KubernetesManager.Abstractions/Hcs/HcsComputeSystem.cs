namespace Redpoint.KubernetesManager.Models.Hcs
{
    using System.Text.Json.Serialization;

    public class HcsComputeSystem
    {
        [JsonPropertyName("SystemType")]
        public string SystemType { get; set; } = string.Empty;

        [JsonPropertyName("State")]
        public string State { get; set; } = string.Empty;
    }
}
