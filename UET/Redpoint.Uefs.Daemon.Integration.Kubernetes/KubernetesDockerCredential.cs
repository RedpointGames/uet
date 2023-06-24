namespace Redpoint.Uefs.Daemon.Integration.Kubernetes
{
    using System.Text.Json.Serialization;

    internal class KubernetesDockerCredential
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }
}
