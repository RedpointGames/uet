namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Sequence
{
    using Redpoint.KubernetesManager.Configuration.Types;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    internal class AtomicSequenceProvisioningStepConfig
    {
        [JsonPropertyName("steps")]
        public IList<RkmNodeProvisionerStep?>? Steps { get; set; }
    }
}
