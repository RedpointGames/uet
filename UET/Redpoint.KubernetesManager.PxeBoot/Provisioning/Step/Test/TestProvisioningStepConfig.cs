namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test
{
    using System.Text.Json.Serialization;

    internal class TestProvisioningStepConfig
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }
}
