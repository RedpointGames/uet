using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;

namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step
{
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.DeleteBootLoaderEntry;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.ExecuteProcess;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Reboot;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Sequence;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.SetFileContent;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.UploadFiles;
    using Redpoint.RuntimeJson;
    using System.Text.Json.Serialization;

    [RuntimeJsonProvider(typeof(ProvisioningStepConfigJsonSerializerContext))]
    [JsonSerializable(typeof(TestProvisioningStepConfig))]
    [JsonSerializable(typeof(EmptyProvisioningStepConfig))]
    [JsonSerializable(typeof(RebootProvisioningStepConfig))]
    [JsonSerializable(typeof(ExecuteProcessProvisioningStepConfig))]
    [JsonSerializable(typeof(AtomicSequenceProvisioningStepConfig))]
    [JsonSerializable(typeof(UploadFilesProvisioningStepConfig))]
    [JsonSerializable(typeof(ModifyFilesProvisioningStepConfig))]
    [JsonSerializable(typeof(DeleteBootLoaderEntryProvisioningStepConfig))]
    internal partial class ProvisioningStepConfigRuntimeJson
    {
    }
}
