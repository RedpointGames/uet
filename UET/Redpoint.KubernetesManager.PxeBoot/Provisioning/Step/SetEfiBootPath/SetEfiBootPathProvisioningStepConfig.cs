namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.SetEfiBootPath
{
    using System.Text.Json.Serialization;

    internal class SetEfiBootPathProvisioningStepConfig
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;
    }
}
