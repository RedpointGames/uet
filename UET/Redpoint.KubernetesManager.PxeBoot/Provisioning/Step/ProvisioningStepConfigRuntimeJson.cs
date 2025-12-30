using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;

namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step
{
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test;
    using Redpoint.RuntimeJson;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    [RuntimeJsonProvider(typeof(ProvisioningStepConfigJsonSerializerContext))]
    [JsonSerializable(typeof(TestProvisioningStepConfig))]
    [JsonSerializable(typeof(EmptyProvisioningStepConfig))]
    [JsonSerializable(typeof(RebootProvisioningStepConfig))]
    internal partial class ProvisioningStepConfigRuntimeJson
    {
    }
}
