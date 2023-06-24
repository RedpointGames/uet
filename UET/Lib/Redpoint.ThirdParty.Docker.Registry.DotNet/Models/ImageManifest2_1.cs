namespace Docker.Registry.DotNet.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Image Manifest Version 2, Schema 1
    /// </summary>

    public class ImageManifest2_1 : ImageManifest
    {
        /// <summary>
        /// name is the name of the image’s repository
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// tag is the tag of the image
        /// </summary>
        [JsonPropertyName("tag")]
        public string Tag { get; set; }

        /// <summary>
        /// architecture is the host architecture on which this image is intended to run. This is for information purposes and not currently used by the engine
        /// </summary>
        [JsonPropertyName("architecture")]
        public string Architecture { get; set; }

        /// <summary>
        /// fsLayers is a list of filesystem layer blob sums contained in this image.
        /// </summary>
        [JsonPropertyName("fsLayers")]
        public ManifestFsLayer[] FsLayers { get; set; }

        /// <summary>
        /// history is a list of unstructured historical data for v1 compatibility. It contains ID of the image layer and ID of the layer’s parent layers.
        /// </summary>
        [JsonPropertyName("history")]
        public ManifestHistory[] History { get; set; }

        /// <summary>
        /// Signed manifests provides an envelope for a signed image manifest. A signed manifest consists of an image manifest along with an additional field containing the signature of the manifest.
        /// The docker client can verify signed manifests and displays a message to the user.
        /// </summary>
        [JsonPropertyName("signatures")]
        public ManifestSignature[] Signatures { get; set; }
    }
}