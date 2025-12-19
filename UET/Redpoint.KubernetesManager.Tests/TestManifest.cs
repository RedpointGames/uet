namespace Redpoint.KubernetesManager.Tests
{
    using Redpoint.KubernetesManager.Manifest;
    using System.Text.Json.Serialization;

    internal class TestManifest : IVersionedManifest
    {
        [JsonIgnore]
        public static int ManifestCurrentVersion => 1;

        [JsonPropertyName("manifestVersion")]
        public int ManifestVersion { get; } = 1;

        [JsonPropertyName("value")]
        public long Value { get; set; }
    }
}
