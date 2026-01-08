namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Text.Json.Serialization;

    public class PatchRkmNodeSpecForceReprovision
    {
        [JsonPropertyName("forceReprovision")]
        public bool ForceReprovision { get; set; }
    }
}
