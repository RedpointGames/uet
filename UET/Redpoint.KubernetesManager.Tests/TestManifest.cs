namespace Redpoint.KubernetesManager.Tests
{
    using System.Text.Json.Serialization;

    internal class TestManifest
    {
        [JsonPropertyName("value")]
        public long Value { get; set; }
    }
}
