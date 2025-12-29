namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Text.Json.Serialization;

    public class RkmNodeGroupSpec
    {
        [JsonPropertyName("provisioner")]
        public string? Provisioner { get; set; }

        [JsonPropertyName("provisionerArguments")]
        public Dictionary<string, string?>? ProvisionerArguments { get; set; }

        [JsonPropertyName("clusterControllerIpAddress")]
        public string? ClusterControllerIpAddress { get; set; }
    }
}
