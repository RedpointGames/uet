namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test
{
    using System.Text.Json.Serialization;

    internal class TestProvisioningStepConfig
    {
        [JsonPropertyName("test")]
        public required string Test { get; set; }
    }
}
