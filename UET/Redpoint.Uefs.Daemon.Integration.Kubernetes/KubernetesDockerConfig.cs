namespace Redpoint.Uefs.Daemon.Integration.Kubernetes
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    internal class KubernetesDockerConfig
    {
        [JsonPropertyName("auths")]
        public Dictionary<string, KubernetesDockerCredential> Auths { get; set; } = new Dictionary<string, KubernetesDockerCredential>();
    }
}
