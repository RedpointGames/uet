namespace Redpoint.KubernetesManager.PxeBoot.ProvisioningStep
{
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Test;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(TestProvisioningStepConfig))]
    internal partial class ProvisioningStepConfigJsonSerializerContext : JsonSerializerContext
    {
        public static ProvisioningStepConfigJsonSerializerContext WithStringEnum { get; } = new ProvisioningStepConfigJsonSerializerContext(new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        });
    }
}
