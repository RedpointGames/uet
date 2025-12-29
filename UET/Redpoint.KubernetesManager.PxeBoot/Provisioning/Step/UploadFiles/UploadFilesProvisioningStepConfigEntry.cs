namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.UploadFiles
{
    using System.Text.Json.Serialization;

    internal class UploadFilesProvisioningStepConfigEntry
    {
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("target")]
        public string? Target { get; set; }
    }
}
