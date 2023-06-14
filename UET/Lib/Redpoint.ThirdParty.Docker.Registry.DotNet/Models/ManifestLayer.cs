namespace Docker.Registry.DotNet.Models
{
    using System.Runtime.Serialization;

    [DataContract]
    public class ManifestLayer
    {
        /// <summary>
        ///     The MIME type of the referenced object. This should generally be application/vnd.docker.image.rootfs.diff.tar.gzip.
        ///     Layers of type application/vnd.docker.image.rootfs.foreign.diff.tar.gzip may be pulled from a remote location but
        ///     they should never be pushed.
        /// </summary>
        [DataMember(Name = "mediaType")]
        public string MediaType { get; set; }

        /// <summary>
        ///     The size in bytes of the object. This field exists so that a client will have an expected size for the content
        ///     before validating. If the length of the retrieved content does not match the specified length, the content should
        ///     not be trusted.
        /// </summary>
        [DataMember(Name = "size")]
        public long Size { get; set; }

        /// <summary>
        ///     The digest of the content, as defined by the Registry V2 HTTP API Specification.
        /// </summary>
        /// <remarks>https://docs.docker.com/registry/spec/api/#digest-parameter</remarks>
        [DataMember(Name = "digest")]
        public string Digest { get; set; }

        /// <summary>
        ///     Provides a list of URLs from which the content may be fetched. Content should be verified against the digest and
        ///     size.
        ///     This field is optional and uncommon.
        /// </summary>
        [DataMember(Name = "urls")]
        public string[] Urls { get; set; }
    }
}