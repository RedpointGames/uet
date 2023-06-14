namespace Docker.Registry.DotNet.Models
{
    using System.Runtime.Serialization;

    [DataContract]
    public abstract class ImageManifest
    {
        /// <summary>
        /// This field specifies the image manifest schema version as an integer.
        /// </summary>
        [DataMember(Name = "schemaVersion")]
        public int SchemaVersion { get; set; }
    }
}