namespace Docker.Registry.DotNet.Models
{
    /// <summary>
    /// A completed upload response.
    /// </summary>
    public class CompletedUploadResponse
    {
        /// <summary>
        /// The Location will contain the registry URL to access the accepted layer file
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// The DockerContentDigest returns the canonical digest of the uploaded blob which may differ from the provided digest. Most clients may ignore the value but if it is used, the client should verify the value against the uploaded blob data.
        /// </summary>
        public string DockerContentDigest { get; set; }
    }
}