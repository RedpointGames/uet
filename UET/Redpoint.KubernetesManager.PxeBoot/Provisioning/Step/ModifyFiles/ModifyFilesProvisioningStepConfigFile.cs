namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.SetFileContent
{
    using System.Text.Json.Serialization;

    internal class ModifyFilesProvisioningStepConfigFile
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public ModifyFilesProvisioningStepConfigFileAction Action { get; set; } = ModifyFilesProvisioningStepConfigFileAction.SetContents;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("enableReplacements")]
        public bool EnableReplacements { get; set; } = false;
    }
}
