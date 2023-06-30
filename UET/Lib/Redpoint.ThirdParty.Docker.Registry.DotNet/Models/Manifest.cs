namespace Docker.Registry.DotNet.Models
{
    using System.Text.Json.Serialization;

    public class Manifest
    {
        /// <summary>
        ///     The MIME type of the referenced object. This will generally be application/vnd.docker.image.manifest.v2+json, but
        ///     it could also be application/vnd.docker.image.manifest.v1+json if the manifest list references a legacy schema-1
        ///     manifest.
        /// </summary>
        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; }

        /// <summary>
        ///     The size in bytes of the object. This field exists so that a client will have an expected size for the content
        ///     before validating. If the length of the retrieved content does not match the specified length, the content should
        ///     not be trusted.
        /// </summary>
        [JsonPropertyName("size")]
        public int Size { get; set; }

        /// <summary>
        ///     The digest of the content, as defined by the Registry V2 HTTP API Specificiation.
        /// </summary>
        /// <remarks>https://docs.docker.com/registry/spec/api/#digest-parameter</remarks>
        [JsonPropertyName("digest")]
        public string Digest { get; set; }

        /// <summary>
        ///     The platform object describes the platform which the image in the manifest runs on. A full list of valid operating
        ///     system and architecture values are listed in the Go language documentation for $GOOS and $GOARCH
        /// </summary>
        /// <remarks>
        ///     https://golang.org/doc/install/source#environment
        /// </remarks>
        [JsonPropertyName("platform")]
        public Platform Platform { get; set; }
    }
}