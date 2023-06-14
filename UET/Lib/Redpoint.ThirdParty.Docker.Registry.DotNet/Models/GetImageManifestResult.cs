namespace Docker.Registry.DotNet.Models
{
    public class GetImageManifestResult
    {
        internal GetImageManifestResult(string mediaType, ImageManifest manifest, string content)
        {
            this.Manifest = manifest;
            this.Content = content;
            this.MediaType = mediaType;
        }

        public string DockerContentDigest { get; internal set; }

        public string Etag { get; internal set; }

        public string MediaType { get; }

        /// <summary>
        ///     The image manifest
        /// </summary>
        public ImageManifest Manifest { get; }

        /// <summary>
        ///     Gets the original, raw body returned from the server.
        /// </summary>
        public string Content { get; }
    }
}