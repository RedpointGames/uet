namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.DeleteBootLoaderEntry
{
    using System.Text.Json.Serialization;

    internal class DeleteBootLoaderEntryProvisioningStepConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
