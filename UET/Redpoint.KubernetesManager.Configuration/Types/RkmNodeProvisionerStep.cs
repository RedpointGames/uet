namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Text.Json.Serialization;

    public class RkmNodeProvisionerStep
    {
        /// <summary>
        /// Specifies the provisioning step type.
        /// </summary>
        [JsonPropertyName("type"), JsonRequired]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// The dynamic settings associated with this provisioning step.
        /// </summary>
        public object? DynamicSettings { get; set; }
    }
}
