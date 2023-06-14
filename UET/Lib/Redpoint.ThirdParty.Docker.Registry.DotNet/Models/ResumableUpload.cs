namespace Docker.Registry.DotNet.Models
{
    /// <summary>
    /// A resumable upload response.
    /// </summary>
    public class ResumableUpload
    {
        /// <summary>
        /// The location of the created upload. Clients should use the contents verbatim to complete the upload, adding parameters where required.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Range header indicating the progress of the upload. When starting an upload, it will return an empty range, since no content has been received.
        /// </summary>
        public string Range { get; set; }

        /// <summary>
        /// Identifies the docker upload uuid for the current request.
        /// </summary>
        public string DockerUploadUuid { get; set; }
    }
}