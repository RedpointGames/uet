namespace Redpoint.KubernetesManager.Configuration.Sources
{
    using k8s.Models;
    using Redpoint.KubernetesManager.Configuration.Json;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(KubernetesList<RkmNode>))]
    [JsonSerializable(typeof(KubernetesList<RkmNodeGroup>))]
    [JsonSerializable(typeof(KubernetesList<RkmNodeProvisioner>))]
    [JsonSerializable(typeof(KubernetesList<RkmConfiguration>))]
    [JsonSerializable(typeof(PatchRkmNodeFullStatus))]
    [JsonSerializable(typeof(PatchRkmNodePartialUpdate))]
    [JsonSerializable(typeof(PatchRkmNodeForceReprovision))]
    public partial class KubernetesRkmJsonSerializerContext : JsonSerializerContext
    {
        public static readonly KubernetesRkmJsonSerializerContext WithStringEnum = CreateStringEnumWithAdditionalConverters();

        public static KubernetesRkmJsonSerializerContext CreateStringEnumWithAdditionalConverters(params JsonConverter[] additionalConverters)
        {
            ArgumentNullException.ThrowIfNull(additionalConverters);

            var options = new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonStringEnumConverter(),
                    new KubernetesDateTimeOffsetConverter(),
                }
            };
            foreach (var converter in additionalConverters)
            {
                options.Converters.Add(converter);
            }
            return new KubernetesRkmJsonSerializerContext(options);
        }
    }
}
