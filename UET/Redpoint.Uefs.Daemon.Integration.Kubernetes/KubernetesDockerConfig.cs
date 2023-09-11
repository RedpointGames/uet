namespace Redpoint.Uefs.Daemon.Integration.Kubernetes
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    internal sealed class KubernetesDockerConfig
    {
        [JsonPropertyName("auths")]
        public Dictionary<string, KubernetesDockerCredential> Auths { get; set; } = new Dictionary<string, KubernetesDockerCredential>();
    }
}
