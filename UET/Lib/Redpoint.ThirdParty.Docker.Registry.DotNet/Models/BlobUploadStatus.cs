namespace Docker.Registry.DotNet.Models
{
    using JetBrains.Annotations;

    [PublicAPI]
    public class BlobUploadStatus
    {
        /// <summary>
        ///     Range header indicating the progress of the upload. When starting an upload, it will return an empty range, since
        ///     no content has been received.
        /// </summary>
        public string Range { get; set; }

        /// <summary>
        ///     Identifies the docker upload uuid for the current request.
        /// </summary>
        public string DockerUploadUuid { get; set; }
    }
}