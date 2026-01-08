namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Text.Json.Serialization;

    public class PatchRkmNodeForceReprovision
    {
        [JsonPropertyName("spec")]
        public PatchRkmNodeSpecForceReprovision? Spec { get; set; }
    }
}
