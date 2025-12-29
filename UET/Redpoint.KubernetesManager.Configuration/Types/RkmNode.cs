namespace Redpoint.KubernetesManager.Configuration.Types
{
    using k8s;
    using k8s.Models;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public class RkmNode : IKubernetesObject<V1ObjectMeta>
    {
        [JsonPropertyName("apiVersion")]
        public required string ApiVersion { get; set; }

        [JsonPropertyName("kind")]
        public required string Kind { get; set; }

        [JsonPropertyName("metadata")]
        public required V1ObjectMeta Metadata { get; set; }

        [JsonPropertyName("spec")]
        public RkmNodeSpec? Spec { get; set; }

        [JsonPropertyName("status")]
        public RkmNodeStatus? Status { get; set; }
    }
}
