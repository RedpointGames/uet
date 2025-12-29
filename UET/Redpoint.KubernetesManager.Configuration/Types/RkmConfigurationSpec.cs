namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Text.Json.Serialization;

    public class RkmConfigurationSpec
    {
        [JsonPropertyName("componentVersions")]
        public RkmConfigurationComponentVersions? ComponentVersions { get; set; }
    }
}
