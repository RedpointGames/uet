namespace Redpoint.KubernetesManager.PxeBoot.ProvisioningStep
{
    using Redpoint.KubernetesManager.Configuration.Json;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Reboot;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(TestProvisioningStepConfig))]
    [JsonSerializable(typeof(EmptyProvisioningStepConfig))]
    [JsonSerializable(typeof(RebootProvisioningStepConfig))]
    internal partial class ProvisioningStepConfigJsonSerializerContext : JsonSerializerContext
    {
        public static ProvisioningStepConfigJsonSerializerContext WithStringEnum { get; } = new ProvisioningStepConfigJsonSerializerContext(new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter(),
                new KubernetesDateTimeOffsetConverter(),
            }
        });
    }
}
