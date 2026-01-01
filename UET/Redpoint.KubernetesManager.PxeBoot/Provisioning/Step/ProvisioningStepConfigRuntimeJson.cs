using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;

namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step
{
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test;
    using Redpoint.RuntimeJson;
    using System.Text.Json.Serialization;

    [RuntimeJsonProvider(typeof(ProvisioningStepConfigJsonSerializerContext))]
    [JsonSerializable(typeof(TestProvisioningStepConfig))]
    [JsonSerializable(typeof(EmptyProvisioningStepConfig))]
    [JsonSerializable(typeof(RebootProvisioningStepConfig))]
    internal partial class ProvisioningStepConfigRuntimeJson
    {
    }
}
