namespace Redpoint.Uefs.Daemon.Integration.Kubernetes
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(KubernetesDockerConfig))]
    internal sealed partial class KubernetesJsonSerializerContext : JsonSerializerContext
    {
    }
}
