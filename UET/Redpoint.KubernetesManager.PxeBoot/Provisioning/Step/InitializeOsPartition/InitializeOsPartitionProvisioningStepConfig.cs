namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.InitializeOsPartition
{
    using System.Text.Json.Serialization;

    internal class InitializeOsPartitionProvisioningStepConfig
    {
        [JsonPropertyName("filesystem")]
        public InitializeOsPartitionProvisioningStepFilesystem Filesystem { get; set; }
    }
}
