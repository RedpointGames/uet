namespace Redpoint.KubernetesManager.Configuration.Sources
{
    using k8s.Models;
    using Redpoint.KubernetesManager.Configuration.Types;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(KubernetesList<RkmNode>))]
    [JsonSerializable(typeof(KubernetesList<RkmNodeGroup>))]
    [JsonSerializable(typeof(KubernetesList<RkmNodeProvisioner>))]
    [JsonSerializable(typeof(KubernetesList<RkmConfiguration>))]
    public partial class KubernetesRkmJsonSerializerContext : JsonSerializerContext
    {
    }
}
