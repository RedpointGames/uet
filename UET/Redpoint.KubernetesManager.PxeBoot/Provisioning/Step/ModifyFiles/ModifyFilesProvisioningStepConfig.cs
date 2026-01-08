namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.SetFileContent
{
    using System.Text.Json.Serialization;

    internal class ModifyFilesProvisioningStepConfig
    {
        [JsonPropertyName("files")]
        public IList<ModifyFilesProvisioningStepConfigFile> Files { get; set; } = new List<ModifyFilesProvisioningStepConfigFile>();
    }
}
