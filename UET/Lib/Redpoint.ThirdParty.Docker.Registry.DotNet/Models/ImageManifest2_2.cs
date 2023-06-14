namespace Docker.Registry.DotNet.Models
{
    using System.Runtime.Serialization;

    [DataContract]
    public class ImageManifest2_2  : ImageManifest
    {

        /// <summary>
        /// The MIME type of the referenced object
        /// </summary>
        [DataMember(Name = "mediaType")]
        public string MediaType { get; set; }

        /// <summary>
        /// The config field references a configuration object for a container, by digest. This configuration 
        /// item is a JSON blob that the runtime uses to set up the container. This new schema uses a tweaked 
        /// version of this configuration to allow image content-addressability on the daemon side.
        /// </summary>

        [DataMember(Name = "config")]
        public Config Config { get; set; }

        /// <summary>
        /// The layer list is ordered starting from the base image (opposite order of schema1).
        /// </summary>
        [DataMember(Name = "layers")]
        public ManifestLayer[] Layers { get; set; }
    }

   
}