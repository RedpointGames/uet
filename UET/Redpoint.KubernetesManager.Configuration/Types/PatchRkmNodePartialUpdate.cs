namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Text.Json.Serialization;

    public class PatchRkmNodePartialUpdate
    {
        [JsonPropertyName("status")]
        public PatchRkmNodeStatusPartialUpdate? Status { get; set; }
    }
}
