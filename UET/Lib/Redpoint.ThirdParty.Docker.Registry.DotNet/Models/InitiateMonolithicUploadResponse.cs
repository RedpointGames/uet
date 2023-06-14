namespace Docker.Registry.DotNet.Models
{
    public class InitiateMonolithicUploadResponse
    {
        public string Location { get; set; }

        public int ContentLength { get; set; }

        public string DockerUploadUuid { get; set; }
    }
}