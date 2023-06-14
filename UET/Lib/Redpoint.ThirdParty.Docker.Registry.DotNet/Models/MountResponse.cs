namespace Docker.Registry.DotNet.Models
{
    public class MountResponse
    {
        /// <summary>
        /// 
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Identifies the docker upload uuid for the current request.
        /// </summary>
        public string DockerUploadUuid { get; set; }

        /// <summary>
        /// If the blob is successfully mounted Created is true,Otherwise, it is flse
        /// If a mount fails due to invalid repository or digest arguments, the registry will fall back to the standard upload behavior And with the upload URL in the <see cref="Location"/>
        /// </summary>
        public bool Created { get; set; }
    }
}