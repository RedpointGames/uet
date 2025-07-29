namespace Redpoint.KubernetesManager.Tests
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(TestManifest))]
    internal partial class TestManifestJsonSerializerContext : JsonSerializerContext
    {
    }
}
