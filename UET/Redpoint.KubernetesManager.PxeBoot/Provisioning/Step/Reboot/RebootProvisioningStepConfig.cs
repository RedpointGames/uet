namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Reboot
{
    using System.Text.Json.Serialization;

    internal class RebootProvisioningStepConfig
    {
        [JsonPropertyName("ipxeScriptTemplate")]
        public string IpxeScriptTemplate { get; set; } = string.Empty;
    }
}
