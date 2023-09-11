namespace Redpoint.Uefs.Daemon.Integration.Kubernetes
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(KubernetesDockerConfig))]
    internal partial sealed class KubernetesJsonSerializerContext : JsonSerializerContext
    {
    }
}
