namespace Docker.Registry.DotNet.Models
{
    public class PushManifestResponse
    {
        /// <summary>
        /// The canonical location url of the uploaded manifest.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// The Content-Length header must be zero and the body must be empty.
        /// </summary>
        public string ContentLength { get; set; }

        /// <summary>
        /// Digest of the targeted content for the request.
        /// </summary>
        public string DockerContentDigest { get; set; }
    }
}
