namespace Docker.Registry.DotNet.Models
{
    using System.Runtime.Serialization;

    /// <summary>
    ///     The manifest list is the “fat manifest” which points to specific image manifests for one or more platforms. Its use
    ///     is optional, and relatively few images will use one of these manifests. A client will distinguish a manifest list
    ///     from an image manifest based on the Content-Type returned in the HTTP response.
    /// </summary>
    public class ManifestList : ImageManifest
    {
        /// <summary>
        ///     The MIME type of the manifest list. This should be set to
        ///     application/vnd.docker.distribution.manifest.list.v2+json.
        /// </summary>
        [DataMember(Name = "mediaType")]
        public string MediaType { get; set; }

        /// <summary>
        ///     The manifests field contains a list of manifests for specific platforms.
        /// </summary>
        [DataMember(Name = "manifests")]
        public Manifest[] Manifests { get; set; }
    }
}