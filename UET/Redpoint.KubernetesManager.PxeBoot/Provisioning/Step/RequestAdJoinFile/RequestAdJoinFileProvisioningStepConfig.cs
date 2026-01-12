namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.RequestAdJoinFile
{
    using System.Text.Json.Serialization;

    internal class RequestAdJoinFileProvisioningStepConfig
    {
        [JsonPropertyName("activeDirectoryIssuerAddress")]
        public string ActiveDirectoryIssuerAddress { get; set; } = string.Empty;

        [JsonPropertyName("outputPath")]
        public string OutputPath { get; set; } = string.Empty;
    }
}
