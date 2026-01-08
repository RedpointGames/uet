namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using System.Text.Json.Serialization;

    internal class WindowsRkmProvisionContext
    {
        [JsonPropertyName("isInRecovery")]
        public required bool IsInRecovery { get; init; }

        [JsonPropertyName("apiAddress")]
        public required string ApiAddress { get; set; }

        [JsonPropertyName("bootedFromStepIndex")]
        public required int BootedFromStepIndex { get; init; }
    }
}
