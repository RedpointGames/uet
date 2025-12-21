namespace Redpoint.KubernetesManager.Tests
{
    using Redpoint.KubernetesManager.Manifest;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    internal class TestManifest : IVersionedManifest
    {
        /**
         * The constructor magic below ensures the JSON deserializer does not require 'required' properties to be set on deserialization, while still requiring us to set them when constructing manifests. This ensures backwards compatibility when attempting to deserialize newer manifests.
         */

#pragma warning disable CS8618
        [JsonConstructor]
        [SetsRequiredMembers]
        internal TestManifest(int manifestVersion)
        {
            ManifestVersion = manifestVersion;
        }
#pragma warning restore CS8618

        public TestManifest()
        {
        }

        [JsonIgnore]
        public static int ManifestCurrentVersion => 1;

        [JsonPropertyName("manifestVersion")]
        public required int ManifestVersion { get; set; } = 1;

        [JsonPropertyName("value")]
        public required long? Value { get; set; }
    }
}
