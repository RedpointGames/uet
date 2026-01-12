namespace Redpoint.KubernetesManager.PxeBoot.ProvisioningStep
{
    using Redpoint.KubernetesManager.Configuration.Json;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.DeleteBootLoaderEntry;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.ExecuteProcess;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.InitializeOsPartition;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Reboot;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.RequestAdJoinFile;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Sequence;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.SetEfiBootPath;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.SetFileContent;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.UploadFiles;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(TestProvisioningStepConfig))]
    [JsonSerializable(typeof(EmptyProvisioningStepConfig))]
    [JsonSerializable(typeof(RebootProvisioningStepConfig))]
    [JsonSerializable(typeof(ExecuteProcessProvisioningStepConfig))]
    [JsonSerializable(typeof(AtomicSequenceProvisioningStepConfig))]
    [JsonSerializable(typeof(UploadFilesProvisioningStepConfig))]
    [JsonSerializable(typeof(ModifyFilesProvisioningStepConfig))]
    [JsonSerializable(typeof(DeleteBootLoaderEntryProvisioningStepConfig))]
    [JsonSerializable(typeof(InitializeOsPartitionProvisioningStepConfig))]
    [JsonSerializable(typeof(SetEfiBootPathProvisioningStepConfig))]
    [JsonSerializable(typeof(RequestAdJoinFileProvisioningStepConfig))]
    internal partial class ProvisioningStepConfigJsonSerializerContext : JsonSerializerContext
    {
    }
}
