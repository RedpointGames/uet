namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Text.Json.Serialization;

    public class RkmNodeGroupSpec
    {
        [JsonPropertyName("provisioner")]
        public string? Provisioner { get; set; }

        [JsonPropertyName("activeDirectory")]
        public RkmNodeGroupActiveDirectory? ActiveDirectory { get; set; }
    }
}
