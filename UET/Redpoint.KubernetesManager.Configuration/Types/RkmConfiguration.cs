namespace Redpoint.KubernetesManager.Configuration.Types
{
    using k8s;
    using k8s.Models;
    using System.Text.Json.Serialization;

    public class RkmConfiguration : IKubernetesObject<V1ObjectMeta>
    {
        [JsonPropertyName("apiVersion")]
        public required string ApiVersion { get; set; }

        [JsonPropertyName("kind")]
        public required string Kind { get; set; }

        [JsonPropertyName("metadata")]
        public required V1ObjectMeta Metadata { get; set; }

        [JsonPropertyName("spec")]
        public RkmConfigurationSpec? Spec { get; set; }
    }
}
