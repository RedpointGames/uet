namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Text.Json.Serialization;

    public class PatchRkmNodeFullStatus
    {
        [JsonPropertyName("status")]
        public RkmNodeStatus? Status { get; set; }
    }
}
