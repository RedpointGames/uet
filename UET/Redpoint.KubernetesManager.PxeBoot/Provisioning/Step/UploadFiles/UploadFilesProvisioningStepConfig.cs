namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.UploadFiles
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    internal class UploadFilesProvisioningStepConfig
    {
        [JsonPropertyName("files")]
        public IList<UploadFilesProvisioningStepConfigEntry?>? Files { get; set; }
    }
}
